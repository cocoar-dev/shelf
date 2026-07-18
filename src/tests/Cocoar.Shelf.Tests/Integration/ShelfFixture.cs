using System.Net.Http.Json;
using System.Text.Json;
using Cocoar.Shelf.Models;
using Marten;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Cocoar.Shelf.Tests.Integration;

public class ShelfFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string DocsRoot { get; private set; } = null!;
    public string ConfigRoot { get; private set; } = null!;
    private string _webRoot = null!;
    public string ApiKey => "test-api-key";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public async Task InitializeAsync()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"shelf-integration-{Guid.NewGuid():N}");
        DocsRoot = Path.Combine(baseDir, "docs");
        ConfigRoot = Path.Combine(baseDir, "config");

        Directory.CreateDirectory(DocsRoot);
        Directory.CreateDirectory(Path.Combine(ConfigRoot, "products"));

        // The landing page and admin SPA fallback serve wwwroot/index.html. The real
        // bundle only exists after a client build (not in the backend-only CI job), so
        // the tests run against their own web root with a stub — they assert routing
        // and fallback behavior, not bundle content.
        _webRoot = Path.Combine(baseDir, "wwwroot");
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"),
            "<!DOCTYPE html><html><head><title>Shelf</title></head><body><div id=\"app\"></div></body></html>");

        await _postgres.StartAsync();

        // Set env vars that FromEnvironment("Shelf__") picks up (overrides configuration.json)
        Environment.SetEnvironmentVariable("Shelf__DocsRoot", DocsRoot);
        Environment.SetEnvironmentVariable("Shelf__ConfigRoot", ConfigRoot);
        Environment.SetEnvironmentVariable("Shelf__ApiKey", ApiKey);
        Environment.SetEnvironmentVariable("Shelf__Database__ConnectionString", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Shelf__TestAuth", "true");
    }

    public new async Task DisposeAsync()
    {
        base.Dispose();

        Environment.SetEnvironmentVariable("Shelf__DocsRoot", null);
        Environment.SetEnvironmentVariable("Shelf__ConfigRoot", null);
        Environment.SetEnvironmentVariable("Shelf__ApiKey", null);
        Environment.SetEnvironmentVariable("Shelf__Database__ConnectionString", null);
        Environment.SetEnvironmentVariable("Shelf__TestAuth", null);

        await _postgres.DisposeAsync();

        try
        {
            var baseDir = Path.GetDirectoryName(DocsRoot)!;
            Directory.Delete(baseDir, recursive: true);
        }
        catch { /* best-effort */ }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseWebRoot(_webRoot);
    }

    /// <summary>Signs in via the test-auth seam and returns a cookie-carrying client.</summary>
    public async Task<HttpClient> CreateSignedInClientAsync(string email, bool admin = false)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/_api/test/signin",
            new { email, displayName = "Test", admin });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Test sign-in failed: {response.StatusCode}");
        return client;
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

    // Products live in the DB now (MartenProductConfigService); JSON files are only migrated at
    // startup, so test setup writes straight to the store.
    public void RegisterProduct(string name, string? displayName = null, string? description = null)
    {
        var store = Services.GetRequiredService<IDocumentStore>();
        using var session = store.LightweightSession();
        if (session.LoadAsync<ProductConfig>(name).GetAwaiter().GetResult() is not null) return;
        session.Store(new ProductConfig
        {
            Name = name,
            DisplayName = displayName ?? name,
            Description = description ?? "",
            Source = "upload",
        });
        session.SaveChangesAsync().GetAwaiter().GetResult();
    }

    public void CreateVersionDirectory(string product, string version, string? indexHtml = null)
    {
        var dir = Path.Combine(DocsRoot, product, version);
        Directory.CreateDirectory(dir);

        if (indexHtml != null)
            File.WriteAllText(Path.Combine(dir, "index.html"), indexHtml);
    }
}
