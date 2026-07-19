using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Cocoar.Shelf.Tests.Integration;

[Collection("Integration")]
public class ApiProductsTests
{
    private readonly HttpClient _client;
    private readonly ShelfFixture _fixture;

    public ApiProductsTests(ShelfFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetProducts_ReturnsRegisteredProduct()
    {
        await _fixture.RegisterProductViaApi(_client, "test-list", "Test Product", "A test");
        _fixture.CreateVersionDirectory("test-list", "v1", "<html></html>");

        var response = await _client.GetAsync("/_api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var products = JsonDocument.Parse(json).RootElement;

        var product = products.EnumerateArray().FirstOrDefault(p => p.GetProperty("name").GetString() == "test-list");
        Assert.Equal("Test Product", product.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task CreateProduct_PersistsOpennessAndRepositoryUrl()
    {
        await CreateProductViaApi(new
        {
            name = "test-oss",
            displayName = "OSS Product",
            source = "upload",
            openness = "OpenSource",
            repositoryUrl = "https://github.com/cocoar-dev/example"
        });

        var product = await GetProduct("test-oss");
        Assert.Equal("OpenSource", product.GetProperty("openness").GetString());
        Assert.Equal("https://github.com/cocoar-dev/example", product.GetProperty("repositoryUrl").GetString());
    }

    [Fact]
    public async Task CreateProduct_DefaultsToUnspecified_WithNoRepositoryUrl()
    {
        await CreateProductViaApi(new { name = "test-default-openness", source = "upload" });

        var product = await GetProduct("test-default-openness");
        Assert.Equal("Unspecified", product.GetProperty("openness").GetString());
        Assert.Equal(JsonValueKind.Null, product.GetProperty("repositoryUrl").ValueKind);
    }

    [Fact]
    public async Task UpdateProduct_ClearsRepositoryUrl_WhenEmptyString()
    {
        await CreateProductViaApi(new
        {
            name = "test-clear-repo",
            source = "upload",
            openness = "Proprietary",
            repositoryUrl = "https://example.com/private"
        });

        // Empty string clears the repo link; the openness marker is left untouched.
        await UpdateProductViaApi("test-clear-repo", new { repositoryUrl = "" });

        var product = await GetProduct("test-clear-repo");
        Assert.Equal(JsonValueKind.Null, product.GetProperty("repositoryUrl").ValueKind);
        Assert.Equal("Proprietary", product.GetProperty("openness").GetString());
    }

    private async Task CreateProductViaApi(object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/_api/products")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _fixture.ApiKey);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task UpdateProductViaApi(string name, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/_api/products/{name}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _fixture.ApiKey);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<JsonElement> GetProduct(string name)
    {
        var response = await _client.GetAsync($"/_api/products/{name}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public async Task GetVersions_Returns404_WhenProductNotRegistered()
    {
        var response = await _client.GetAsync("/_api/products/nonexistent/versions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("not registered", json);
    }

    [Fact]
    public async Task GetVersions_ReturnsVersions_ForRegisteredProduct()
    {
        await _fixture.RegisterProductViaApi(_client, "test-versions");
        _fixture.CreateVersionDirectory("test-versions", "v1", "<html></html>");
        _fixture.CreateVersionDirectory("test-versions", "v2", "<html></html>");

        var response = await _client.GetAsync("/_api/products/test-versions/versions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;

        var versions = doc.GetProperty("versions").EnumerateArray().Select(v => v.GetString()).ToList();
        Assert.Contains("v1", versions);
        Assert.Contains("v2", versions);
    }
}
