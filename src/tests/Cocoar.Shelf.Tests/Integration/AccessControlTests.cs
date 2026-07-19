using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cocoar.Shelf.Models;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Cocoar.Shelf.Tests.Integration;

/// <summary>
/// Access Control v2: restricted-product gating in the docs middleware and product API, group-based
/// read grants and adminship, and JsEval auto-membership end-to-end (login → materialized membership).
/// </summary>
[Collection("Integration")]
public class AccessControlTests
{
    private readonly ShelfFixture _fixture;

    public AccessControlTests(ShelfFixture fixture) => _fixture = fixture;

    private void StoreProduct(string name, bool restricted, params PrincipalRef[] readPrincipals)
    {
        var store = _fixture.Services.GetRequiredService<IDocumentStore>();
        using var session = store.LightweightSession();
        session.Store(new ProductConfig
        {
            Name = name,
            DisplayName = name,
            Source = "upload",
            Visibility = "public",
            Restricted = restricted,
            ReadPrincipals = readPrincipals.ToList(),
        });
        session.SaveChangesAsync().GetAwaiter().GetResult();
    }

    private void StoreGroup(Group group)
    {
        var store = _fixture.Services.GetRequiredService<IDocumentStore>();
        using var session = store.LightweightSession();
        session.Store(group);
        session.SaveChangesAsync().GetAwaiter().GetResult();
    }

    // ---- Docs middleware gating ----

    [Fact]
    public async Task RestrictedDocs_Anonymous_HtmlRedirectsToLogin()
    {
        _fixture.CreateVersionDirectory("ac-anon-html", "v1", "<html>secret</html>");
        StoreProduct("ac-anon-html", restricted: true);
        var client = _fixture.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");

        var response = await client.GetAsync("/ac-anon-html/v1/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString() ?? "");
    }

    [Fact]
    public async Task RestrictedDocs_Anonymous_AssetReturns401()
    {
        _fixture.CreateVersionDirectory("ac-anon-asset", "v1", "<html></html>");
        File.WriteAllText(Path.Combine(_fixture.DocsRoot, "ac-anon-asset", "v1", "app.js"), "console.log(1)");
        StoreProduct("ac-anon-asset", restricted: true);
        var client = _fixture.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/ac-anon-asset/v1/app.js");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RestrictedDocs_AuthenticatedNonMember_Returns404()
    {
        _fixture.CreateVersionDirectory("ac-nonmember", "v1", "<html>secret</html>");
        StoreProduct("ac-nonmember", restricted: true);
        var client = await _fixture.CreateSignedInClientAsync("ac-outsider@shelf.test");

        var response = await client.GetAsync("/ac-nonmember/v1/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RestrictedDocs_GroupMemberByEmail_IsServed()
    {
        var groupId = Guid.NewGuid();
        StoreGroup(new Group { Id = groupId, Name = "ac-member-grp", MemberEmails = ["ac-insider@shelf.test"] });
        _fixture.CreateVersionDirectory("ac-member", "v1", "<html>secret</html>");
        StoreProduct("ac-member", restricted: true, PrincipalRef.ForGroup(groupId));
        var client = await _fixture.CreateSignedInClientAsync("ac-insider@shelf.test");

        var response = await client.GetAsync("/ac-member/v1/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("secret", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RestrictedDocs_DirectUserGrant_IsServed()
    {
        _fixture.CreateVersionDirectory("ac-direct-user", "v1", "<html>secret</html>");
        // Grant a single user directly (by email) — no group needed.
        StoreProduct("ac-direct-user", restricted: true, PrincipalRef.ForUser("ac-direct@shelf.test"));
        var client = await _fixture.CreateSignedInClientAsync("ac-direct@shelf.test");

        var response = await client.GetAsync("/ac-direct-user/v1/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RestrictedDocs_Admin_IsServed()
    {
        _fixture.CreateVersionDirectory("ac-admin", "v1", "<html>secret</html>");
        StoreProduct("ac-admin", restricted: true);
        var client = await _fixture.CreateSignedInClientAsync("ac-admin@shelf.test", admin: true);

        var response = await client.GetAsync("/ac-admin/v1/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Product API filtering ----

    [Fact]
    public async Task ProductsApi_HidesRestricted_FromNonMember()
    {
        StoreProduct("ac-hidden", restricted: true);
        var client = await _fixture.CreateSignedInClientAsync("ac-listing-outsider@shelf.test");

        var products = await client.GetFromJsonAsync<List<ProductDto>>("/_api/products");

        Assert.DoesNotContain(products!, p => p.Name == "ac-hidden");
    }

    [Fact]
    public async Task ProductsApi_ShowsRestricted_ToAdmin()
    {
        StoreProduct("ac-visible-admin", restricted: true);
        var client = await _fixture.CreateSignedInClientAsync("ac-listing-admin@shelf.test", admin: true);

        var products = await client.GetFromJsonAsync<List<ProductDto>>("/_api/products");

        var product = Assert.Single(products!, p => p.Name == "ac-visible-admin");
        Assert.True(product.Restricted);
    }

    [Fact]
    public async Task ProductApi_Single_Returns404_ForNonMember()
    {
        StoreProduct("ac-single-hidden", restricted: true);
        var client = await _fixture.CreateSignedInClientAsync("ac-single-outsider@shelf.test");

        var response = await client.GetAsync("/_api/products/ac-single-hidden");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LlmsTxt_ExcludesRestricted()
    {
        StoreProduct("ac-llms-restricted", restricted: true);
        _fixture.CreateVersionDirectory("ac-llms-restricted", "v1.0.0", "<html></html>");
        var client = _fixture.CreateClient();

        var body = await client.GetStringAsync("/llms.txt");

        Assert.DoesNotContain("ac-llms-restricted", body);
    }

    // ---- Group adminship ----

    [Fact]
    public async Task AdminGroupMember_IsAdmin_AndCanWriteProducts()
    {
        StoreGroup(new Group
        {
            Id = Guid.NewGuid(),
            Name = "ac-admin-grp",
            MemberEmails = ["ac-grpadmin@shelf.test"],
            IsAdminGroup = true,
        });
        // Plain sign-in (no allowlist/token admin) — adminship must come from the group alone.
        var client = await _fixture.CreateSignedInClientAsync("ac-grpadmin@shelf.test");

        var me = await client.GetFromJsonAsync<MeDto>("/_api/auth/me");
        Assert.True(me!.IsAdmin);

        var write = await client.PostAsJsonAsync("/_api/products", new { name = "ac-grpadmin-write" });
        Assert.Equal(HttpStatusCode.Created, write.StatusCode);
    }

    // ---- JsEval auto-membership, end to end ----

    [Fact]
    public async Task AutoMembership_GrantsAccess_AfterMatchingUserLogsIn()
    {
        var admin = await _fixture.CreateSignedInClientAsync("ac-auto-admin@shelf.test", admin: true);

        // Auto group: predicate matches by email domain.
        var create = await admin.PostAsJsonAsync("/_api/groups", new
        {
            name = "ac-auto-grp",
            membershipMode = "Auto",
            membershipScript = "user.email.endsWith('@ac-auto.test')",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var group = await create.Content.ReadFromJsonAsync<GroupIdDto>();

        // Grant the group read access on the restricted product (product-side principal grant).
        _fixture.CreateVersionDirectory("ac-auto-prod", "v1", "<html>auto-secret</html>");
        StoreProduct("ac-auto-prod", restricted: true, PrincipalRef.ForGroup(group!.Id));

        // Matching user signs in → login recompute materializes membership.
        var member = await _fixture.CreateSignedInClientAsync("someone@ac-auto.test");
        var served = await member.GetAsync("/ac-auto-prod/v1/");
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        // Non-matching user stays out.
        var outsider = await _fixture.CreateSignedInClientAsync("someone@other.test");
        var denied = await outsider.GetAsync("/ac-auto-prod/v1/");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Fact]
    public async Task Groups_Endpoints_AreAdminGated()
    {
        var client = await _fixture.CreateSignedInClientAsync("ac-groups-plain@shelf.test");

        var response = await client.GetAsync("/_api/groups");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestScript_DryRun_ReportsMatch()
    {
        var admin = await _fixture.CreateSignedInClientAsync("ac-dryrun-admin@shelf.test", admin: true);

        var response = await admin.PostAsJsonAsync("/_api/groups/test-script", new
        {
            script = "user.email.endsWith('@shelf.test')",
            email = "ac-dryrun-admin@shelf.test",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TestScriptDto>();
        Assert.True(result!.Matched);
        Assert.Null(result.Error);
    }

    private sealed record ProductDto(string Name, bool Restricted);
    private sealed record MeDto(bool IsAdmin);
    private sealed record TestScriptDto(bool Matched, string? Error);
    private sealed record GroupIdDto(Guid Id);
}
