using System.Net;
using System.Net.Http.Headers;

namespace Cocoar.Shelf.Tests.Integration;

[Collection("Integration")]
public class DocsServingTests
{
    private readonly HttpClient _client;
    private readonly ShelfFixture _fixture;

    public DocsServingTests(ShelfFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient(new() { AllowAutoRedirect = false });
        _fixture.RegisterProductViaApi(_client, "docs-test").GetAwaiter().GetResult();
    }

    private async Task UploadVersion(string product, string version, params (string name, string content)[] entries)
    {
        using var zip = ZipHelper.Create(entries);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/_api/products/{product}/versions/{version}")
        {
            Content = ZipHelper.ToContent(zip)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _fixture.ApiKey);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ServesIndexHtml_ForVersionRoot()
    {
        await UploadVersion("docs-test", "v20", ("index.html", "<html><body>v20</body></html>"));

        var response = await _client.GetAsync("/docs-test/v20/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("v20", content);
    }

    [Fact]
    public async Task ServesSubPage()
    {
        await UploadVersion("docs-test", "v21",
            ("index.html", "<html></html>"),
            ("guide/getting-started.html", "<html><body>Guide</body></html>"));

        var response = await _client.GetAsync("/docs-test/v21/guide/getting-started.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Guide", content);
    }

    [Fact]
    public async Task RedirectsToLatest_WhenNoVersionSpecified()
    {
        await UploadVersion("docs-test", "v22", ("index.html", "<html></html>"));

        var response = await _client.GetAsync("/docs-test/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/docs-test/", response.Headers.Location?.ToString() ?? "");
    }

    [Fact]
    public async Task Returns404_ForNonExistentFile()
    {
        await UploadVersion("docs-test", "v23", ("index.html", "<html></html>"));

        var response = await _client.GetAsync("/docs-test/v23/nonexistent.html");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RewritesBasePath_InHtml()
    {
        await UploadVersion("docs-test", "v24",
            ("index.html", """<html><head><link href="/assets/style.css" rel="stylesheet"></head><body>Test</body></html>"""));

        var response = await _client.GetAsync("/docs-test/v24/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains("href=\"/docs-test/v24/assets/style.css\"", content);
        Assert.DoesNotContain("href=\"/assets/style.css\"", content);
    }

    [Fact]
    public async Task SetsImmutableCacheHeaders_ForHashedAssets()
    {
        await UploadVersion("docs-test", "v25",
            ("index.html", "<html></html>"),
            ("assets/style.a1b2c3.css", "body{}"));

        var response = await _client.GetAsync("/docs-test/v25/assets/style.a1b2c3.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cacheControl = response.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl.Public);
        Assert.Equal(TimeSpan.FromSeconds(31536000), cacheControl.MaxAge);
    }

    [Fact]
    public async Task ServesPlainTextFiles()
    {
        await UploadVersion("docs-test", "v26",
            ("index.html", "<html></html>"),
            ("llms.txt", "# Documentation for LLMs"));

        var response = await _client.GetAsync("/docs-test/v26/llms.txt");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("# Documentation for LLMs", content);
    }

    [Fact]
    public async Task LandingPage_Returns200()
    {
        var autoRedirectClient = _fixture.CreateClient();

        var response = await autoRedirectClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("html", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LatestRedirect_PrefersStableOverPreRelease()
    {
        await _fixture.RegisterProductViaApi(_client, "stable-test");
        await UploadVersion("stable-test", "v2.0.0", ("index.html", "<html>stable</html>"));
        await UploadVersion("stable-test", "v3.0.0-beta.1", ("index.html", "<html>beta</html>"));

        var response = await _client.GetAsync("/stable-test/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("v2.0.0", location);
        Assert.DoesNotContain("beta", location);
    }
}
