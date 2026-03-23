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
