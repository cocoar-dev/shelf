using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Cocoar.Shelf.Tests.Integration;

[Collection("Integration")]
public class AdminApiTests
{
    private readonly HttpClient _client;
    private readonly ShelfFixture _fixture;

    public AdminApiTests(ShelfFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    private HttpRequestMessage Auth(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _fixture.ApiKey);
        return request;
    }

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private async Task<HttpResponseMessage> CreateProductViaApi(string name, string? displayName = null, string? description = null)
    {
        var request = Auth(new HttpRequestMessage(HttpMethod.Post, "/_api/products")
        {
            Content = Json(new { name, displayName, description })
        });
        return await _client.SendAsync(request);
    }

    #region Auth Verify

    [Fact]
    public async Task Verify_Returns200_WithValidKey()
    {
        var request = Auth(new HttpRequestMessage(HttpMethod.Get, "/_api/admin/verify"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Verify_Returns401_WithoutKey()
    {
        var response = await _client.GetAsync("/_api/admin/verify");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Create Product

    [Fact]
    public async Task CreateProduct_Returns201_WithValidData()
    {
        var response = await CreateProductViaApi("admin-create-1", "Create Test", "A test product");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;
        Assert.Equal("admin-create-1", doc.GetProperty("name").GetString());
        Assert.Equal("Create Test", doc.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task CreateProduct_Returns409_WhenAlreadyExists()
    {
        await CreateProductViaApi("admin-dup-1");

        var response = await CreateProductViaApi("admin-dup-1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_Returns400_WithEmptyName()
    {
        var request = Auth(new HttpRequestMessage(HttpMethod.Post, "/_api/products")
        {
            Content = Json(new { name = "" })
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_Returns400_WithInvalidName()
    {
        var request = Auth(new HttpRequestMessage(HttpMethod.Post, "/_api/products")
        {
            Content = Json(new { name = "Invalid Name!" })
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_Returns400_WithReservedName()
    {
        var request = Auth(new HttpRequestMessage(HttpMethod.Post, "/_api/products")
        {
            Content = Json(new { name = "admin" })
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("reserved", json);
    }

    [Fact]
    public async Task CreateProduct_Returns401_WithoutAuth()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/_api/products")
        {
            Content = Json(new { name = "admin-unauth-1" })
        };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_DefaultsSourceToUpload()
    {
        var response = await CreateProductViaApi("admin-default-src-1");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;
        Assert.Equal("upload", doc.GetProperty("source").GetString());
    }

    #endregion

    #region Update Product

    [Fact]
    public async Task UpdateProduct_Returns200_WithValidData()
    {
        await CreateProductViaApi("admin-update-1", "Original");

        var request = Auth(new HttpRequestMessage(HttpMethod.Put, "/_api/products/admin-update-1")
        {
            Content = Json(new { displayName = "Updated Name", description = "New desc" })
        });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;
        Assert.Equal("Updated Name", doc.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task UpdateProduct_Returns404_WhenNotFound()
    {
        var request = Auth(new HttpRequestMessage(HttpMethod.Put, "/_api/products/admin-nonexistent-update")
        {
            Content = Json(new { displayName = "Nope" })
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProduct_PreservesExistingFields_WhenNotProvided()
    {
        await CreateProductViaApi("admin-partial-1", "Original", "Original Desc");

        var request = Auth(new HttpRequestMessage(HttpMethod.Put, "/_api/products/admin-partial-1")
        {
            Content = Json(new { displayName = "Changed" })
        });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;
        Assert.Equal("Changed", doc.GetProperty("displayName").GetString());
        Assert.Equal("Original Desc", doc.GetProperty("description").GetString());
    }

    #endregion

    #region Delete Product

    [Fact]
    public async Task DeleteProduct_Returns204_WhenExists()
    {
        await CreateProductViaApi("admin-delete-1");

        var request = Auth(new HttpRequestMessage(HttpMethod.Delete, "/_api/products/admin-delete-1"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_Returns404_WhenNotFound()
    {
        var request = Auth(new HttpRequestMessage(HttpMethod.Delete, "/_api/products/admin-nonexistent-del"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Delete Version

    [Fact]
    public async Task DeleteVersion_Returns204_WhenExists()
    {
        await CreateProductViaApi("admin-verdel-1");
        _fixture.CreateVersionDirectory("admin-verdel-1", "v1", "<html></html>");
        _fixture.CreateVersionDirectory("admin-verdel-1", "v2", "<html></html>");

        var request = Auth(new HttpRequestMessage(HttpMethod.Delete,
            "/_api/products/admin-verdel-1/versions/v1"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(Directory.Exists(Path.Combine(_fixture.DocsRoot, "admin-verdel-1", "v1")));
        Assert.True(Directory.Exists(Path.Combine(_fixture.DocsRoot, "admin-verdel-1", "v2")));
    }

    [Fact]
    public async Task DeleteVersion_Returns404_WhenVersionNotFound()
    {
        await CreateProductViaApi("admin-ver404-1");

        var request = Auth(new HttpRequestMessage(HttpMethod.Delete,
            "/_api/products/admin-ver404-1/versions/v99"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteVersion_Returns404_WhenProductNotRegistered()
    {
        var request = Auth(new HttpRequestMessage(HttpMethod.Delete,
            "/_api/products/admin-unregistered-verdel/versions/v1"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteVersion_Returns400_WithInvalidVersionFormat()
    {
        await CreateProductViaApi("admin-badver-1");

        var request = Auth(new HttpRequestMessage(HttpMethod.Delete,
            "/_api/products/admin-badver-1/versions/not-a-version"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region SPA Fallback

    [Fact]
    public async Task AdminRoute_ReturnsHtml_ForClientSideRoutes()
    {
        var response = await _client.GetAsync("/admin/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AdminRoute_ReturnsHtml_ForNestedRoutes()
    {
        var response = await _client.GetAsync("/admin/products/some-product");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AdminRoute_DoesNotInterfereWithApi()
    {
        var response = await _client.GetAsync("/_api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    #endregion
}
