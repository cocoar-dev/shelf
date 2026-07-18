using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cocoar.Shelf.Tests.Integration;

/// <summary>
/// Exercises the cookie session + RBAC pipeline on the modgud-federated auth model, using the
/// test-auth seam in place of the external IdP (the suite can't reach modgud).
/// </summary>
[Collection("Integration")]
public class AuthApiTests
{
    private readonly ShelfFixture _fixture;

    public AuthApiTests(ShelfFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Me_Returns401_WhenNotSignedIn()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/_api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestSignIn_MintsCookie_AndMeReturnsUser()
    {
        var client = await _fixture.CreateSignedInClientAsync("plain-user@shelf.test");

        var response = await client.GetAsync("/_api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.True(doc.GetProperty("authenticated").GetBoolean());
        Assert.Equal("plain-user@shelf.test", doc.GetProperty("email").GetString());
        Assert.False(doc.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Me_ReportsAdmin_WithRbacPermission()
    {
        var client = await _fixture.CreateSignedInClientAsync("rbac-admin@shelf.test", admin: true);

        var response = await client.GetAsync("/_api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.True(doc.GetProperty("isAdmin").GetBoolean());
        Assert.Contains("shelf:admin",
            doc.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task UserList_Returns403_ForPlainUser()
    {
        var client = await _fixture.CreateSignedInClientAsync("no-admin@shelf.test");

        var response = await client.GetAsync("/_api/users/");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserList_Returns200_ForRbacAdmin()
    {
        var client = await _fixture.CreateSignedInClientAsync("list-admin@shelf.test", admin: true);

        var response = await client.GetAsync("/_api/users/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Contains("list-admin@shelf.test",
            doc.EnumerateArray().Select(u => u.GetProperty("email").GetString()));
    }

    [Fact]
    public async Task UserList_Returns401_WhenAnonymous()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/_api/users/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var client = await _fixture.CreateSignedInClientAsync("logout-user@shelf.test");

        var logout = await client.PostAsync("/_api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var me = await client.GetAsync("/_api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task LoginPage_IsNotMapped_WithoutModgudClient()
    {
        // The OIDC challenge endpoint only exists when a modgud web client is configured —
        // the test host runs without one (auth comes through the test seam).
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/login");

        // Unmapped → the SPA fallback serves the app shell instead of a challenge redirect.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task LogoutPage_EndsSession_AndRedirectsHome()
    {
        var client = await _fixture.CreateSignedInClientAsync("browser-logout@shelf.test");

        var response = await client.GetAsync("/logout");   // auto-follows the redirect to /

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await client.GetAsync("/_api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task CookieAuth_WorksForProductWrites()
    {
        // The admin UI path: cookie session (not API key) on the product write endpoints.
        var client = await _fixture.CreateSignedInClientAsync("product-writer@shelf.test");

        var response = await client.PostAsJsonAsync("/_api/products",
            new { name = "cookie-write-1" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
