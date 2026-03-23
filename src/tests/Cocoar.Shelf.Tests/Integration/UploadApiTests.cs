using System.Net;
using System.Net.Http.Headers;

namespace Cocoar.Shelf.Tests.Integration;

[Collection("Integration")]
public class UploadApiTests
{
    private readonly HttpClient _client;
    private readonly ShelfFixture _fixture;

    public UploadApiTests(ShelfFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
        _fixture.RegisterProductViaApi(_client, "upload-test").GetAwaiter().GetResult();
    }

    private HttpRequestMessage CreateUploadRequest(string product, string version, HttpContent content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/_api/products/{product}/versions/{version}")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _fixture.ApiKey);
        return request;
    }

    [Fact]
    public async Task Upload_Returns201_WithValidZip()
    {
        using var zip = ZipHelper.Create(("index.html", "<html></html>"), ("assets/app.js", "console.log('hi');"));
        var request = CreateUploadRequest("upload-test", "v10", ZipHelper.ToContent(zip));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(File.Exists(Path.Combine(_fixture.DocsRoot, "upload-test", "v10", "index.html")));
    }

    [Fact]
    public async Task Upload_Returns401_WithoutAuth()
    {
        using var zip = ZipHelper.Create(("index.html", "<html></html>"));
        var request = new HttpRequestMessage(HttpMethod.Post, "/_api/products/upload-test/versions/v11")
        {
            Content = ZipHelper.ToContent(zip)
        };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_Returns401_WithWrongKey()
    {
        using var zip = ZipHelper.Create(("index.html", "<html></html>"));
        var request = new HttpRequestMessage(HttpMethod.Post, "/_api/products/upload-test/versions/v12")
        {
            Content = ZipHelper.ToContent(zip)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-key");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_Returns400_WithInvalidVersionFormat()
    {
        using var zip = ZipHelper.Create(("index.html", "<html></html>"));
        var request = CreateUploadRequest("upload-test", "not-a-version", ZipHelper.ToContent(zip));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid version format", json);
        Assert.Contains("Must match pattern", json);
    }

    [Fact]
    public async Task Upload_Returns404_ForUnregisteredProduct()
    {
        using var zip = ZipHelper.Create(("index.html", "<html></html>"));
        var request = CreateUploadRequest("unregistered-product", "v1", ZipHelper.ToContent(zip));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("not registered", json);
    }

    [Fact]
    public async Task Upload_Returns400_WithCorruptZip()
    {
        var content = new ByteArrayContent("this is not a zip"u8.ToArray());
        content.Headers.ContentType = new("application/zip");
        var request = CreateUploadRequest("upload-test", "v13", content);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid", json);
    }

    [Fact]
    public async Task Upload_Returns400_WithoutIndexHtml()
    {
        using var zip = ZipHelper.Create(("readme.txt", "hello"), ("assets/style.css", "body{}"));
        var request = CreateUploadRequest("upload-test", "v14", ZipHelper.ToContent(zip));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("index.html", json);
    }

    [Fact]
    public async Task Upload_ReplacesExistingVersion()
    {
        using var zip1 = ZipHelper.Create(("index.html", "<html>original</html>"));
        var r1 = CreateUploadRequest("upload-test", "v15", ZipHelper.ToContent(zip1));
        await _client.SendAsync(r1);

        using var zip2 = ZipHelper.Create(("index.html", "<html>updated</html>"));
        var r2 = CreateUploadRequest("upload-test", "v15", ZipHelper.ToContent(zip2));
        var response = await _client.SendAsync(r2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = File.ReadAllText(Path.Combine(_fixture.DocsRoot, "upload-test", "v15", "index.html"));
        Assert.Equal("<html>updated</html>", content);
    }

    [Fact]
    public async Task Upload_AcceptsSemVerVersions()
    {
        var versions = new[] { "v1", "v5.2", "v5.2.0", "5.0.0", "v6.0.0-beta.1" };

        foreach (var version in versions)
        {
            using var zip = ZipHelper.Create(("index.html", $"<html>{version}</html>"));
            var request = CreateUploadRequest("upload-test", version, ZipHelper.ToContent(zip));

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact]
    public async Task Upload_IsVisibleInVersionsList()
    {
        await _fixture.RegisterProductViaApi(_client, "upload-visible");
        using var zip = ZipHelper.Create(("index.html", "<html></html>"));
        var request = CreateUploadRequest("upload-visible", "v3", ZipHelper.ToContent(zip));
        await _client.SendAsync(request);

        var response = await _client.GetAsync("/_api/products/upload-visible/versions");
        var json = await response.Content.ReadAsStringAsync();
        var doc = System.Text.Json.JsonDocument.Parse(json).RootElement;

        Assert.Equal("v3", doc.GetProperty("latest").GetString());
    }
}
