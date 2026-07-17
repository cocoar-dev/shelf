using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cocoar.Shelf.Tests.Integration;

public class ShelfFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string DocsRoot { get; private set; } = null!;
    public string ConfigRoot { get; private set; } = null!;
    public string ApiKey => "test-api-key";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public Task InitializeAsync()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"shelf-integration-{Guid.NewGuid():N}");
        DocsRoot = Path.Combine(baseDir, "docs");
        ConfigRoot = Path.Combine(baseDir, "config");

        Directory.CreateDirectory(DocsRoot);
        Directory.CreateDirectory(Path.Combine(ConfigRoot, "products"));

        // Set env vars that FromEnvironment("Shelf__") picks up (overrides configuration.json)
        Environment.SetEnvironmentVariable("Shelf__DocsRoot", DocsRoot);
        Environment.SetEnvironmentVariable("Shelf__ConfigRoot", ConfigRoot);
        Environment.SetEnvironmentVariable("Shelf__ApiKey", ApiKey);
        Environment.SetEnvironmentVariable("Shelf__Database__ConnectionString", "");

        return Task.CompletedTask;
    }

    public new Task DisposeAsync()
    {
        base.Dispose();

        Environment.SetEnvironmentVariable("Shelf__DocsRoot", null);
        Environment.SetEnvironmentVariable("Shelf__ConfigRoot", null);
        Environment.SetEnvironmentVariable("Shelf__ApiKey", null);
        Environment.SetEnvironmentVariable("Shelf__Database__ConnectionString", null);

        try
        {
            var baseDir = Path.GetDirectoryName(DocsRoot)!;
            Directory.Delete(baseDir, recursive: true);
        }
        catch { /* best-effort */ }

        return Task.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
    }

    public async Task RegisterProductViaApi(HttpClient client, string name, string? displayName = null, string? description = null)
    {
        var body = new { name, displayName = displayName ?? name, description = description ?? "", source = "upload" };
        var request = new HttpRequestMessage(HttpMethod.Post, "/_api/products")
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
        var response = await client.SendAsync(request);

        // 201 = created, 409 = already exists (both OK for test setup)
        if (response.StatusCode != System.Net.HttpStatusCode.Created &&
            response.StatusCode != System.Net.HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException($"Failed to register product '{name}': {response.StatusCode}");
        }
    }

    public void RegisterProduct(string name, string? displayName = null, string? description = null)
    {
        var config = new { name, displayName = displayName ?? name, description = description ?? "", source = "upload" };
        File.WriteAllText(
            Path.Combine(ConfigRoot, "products", $"{name}.json"),
            JsonSerializer.Serialize(config, JsonOptions));
    }

    public void CreateVersionDirectory(string product, string version, string? indexHtml = null)
    {
        var dir = Path.Combine(DocsRoot, product, version);
        Directory.CreateDirectory(dir);

        if (indexHtml != null)
            File.WriteAllText(Path.Combine(dir, "index.html"), indexHtml);
    }
}
