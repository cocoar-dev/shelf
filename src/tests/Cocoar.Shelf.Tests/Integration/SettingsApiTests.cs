using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cocoar.Shelf.Tests.Integration;

[Collection("Integration")]
public class SettingsApiTests
{
    private readonly ShelfFixture _fixture;

    public SettingsApiTests(ShelfFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetSettings_Returns403_ForPlainUser()
    {
        var client = await _fixture.CreateSignedInClientAsync("settings-plain@shelf.test");

        var response = await client.GetAsync("/_api/settings/");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSettings_ReportsKeyState_ForAdmin()
    {
        var client = await _fixture.CreateSignedInClientAsync("settings-admin@shelf.test", admin: true);

        var response = await client.GetAsync("/_api/settings/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        // The fixture always configures the env bootstrap key.
        Assert.True(doc.GetProperty("hasConfigApiKey").GetBoolean());
    }

    [Fact]
    public async Task SetMasterApiKey_Returns400_WhenTooShort()
    {
        var client = await _fixture.CreateSignedInClientAsync("settings-short@shelf.test", admin: true);

        var response = await client.PutAsJsonAsync("/_api/settings/api-key", new { apiKey = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProductApiKey_Reveal_IsAdminOnly()
    {
        _fixture.RegisterProduct("keyreveal-product");

        var anon = _fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.GetAsync("/_api/products/keyreveal-product/api-key")).StatusCode);

        var plain = await _fixture.CreateSignedInClientAsync("keyreveal-plain@shelf.test");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await plain.GetAsync("/_api/products/keyreveal-product/api-key")).StatusCode);

        var admin = await _fixture.CreateSignedInClientAsync("keyreveal-admin@shelf.test", admin: true);
        var update = await admin.PutAsJsonAsync("/_api/products/keyreveal-product",
            new { apiKey = "product-key-abc-123" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var reveal = await admin.GetAsync("/_api/products/keyreveal-product/api-key");
        Assert.Equal(HttpStatusCode.OK, reveal.StatusCode);
        var doc = JsonDocument.Parse(await reveal.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("product-key-abc-123", doc.GetProperty("apiKey").GetString());
    }

    [Fact]
    public async Task MasterApiKey_WorksAsBearer_AndClearRevokesIt()
    {
        var admin = await _fixture.CreateSignedInClientAsync("settings-key-admin@shelf.test", admin: true);
        const string key = "ui-managed-master-key-1234";

        var set = await admin.PutAsJsonAsync("/_api/settings/api-key", new { apiKey = key });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        // Admins can read the key back (docs hosting, not an IdP — keys are recoverable).
        var settings = await admin.GetAsync("/_api/settings/");
        var settingsDoc = JsonDocument.Parse(await settings.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(key, settingsDoc.GetProperty("masterApiKey").GetString());

        // The UI-managed key authorizes CI-style bearer calls...
        var bearer = _fixture.CreateClient();
        var create = new HttpRequestMessage(HttpMethod.Post, "/_api/products")
        {
            Content = JsonContent.Create(new { name = "settings-key-product" }),
        };
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var created = await bearer.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // ...the env bootstrap key still works alongside...
        var envDelete = new HttpRequestMessage(HttpMethod.Delete, "/_api/products/settings-key-product");
        envDelete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _fixture.ApiKey);
        Assert.Equal(HttpStatusCode.NoContent, (await bearer.SendAsync(envDelete)).StatusCode);

        // ...and clearing the UI key revokes it.
        var clear = await admin.PutAsJsonAsync("/_api/settings/api-key", new { apiKey = (string?)null });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);

        var retry = new HttpRequestMessage(HttpMethod.Post, "/_api/products")
        {
            Content = JsonContent.Create(new { name = "settings-key-product-2" }),
        };
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await bearer.SendAsync(retry)).StatusCode);
    }
}
