using System.Net;
using System.Net.Http.Headers;
using Cocoar.Configuration.Reactive;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Cocoar.Shelf.Tests.Integration;

[Collection("Integration")]
public class UploadDisabledTests
{
    private readonly HttpClient _client;

    public UploadDisabledTests(ShelfFixture fixture)
    {
        fixture.RegisterProduct("disabled-test");

        // Create a separate factory that overrides ApiKey to empty
        var factory = fixture.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace the scoped ShelfOptions with one that has no API key
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ShelfOptions));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddScoped(sp =>
                {
                    var config = sp.GetRequiredService<IReactiveConfig<ShelfOptions>>();
                    var opts = config.CurrentValue;
                    return new ShelfOptions
                    {
                        DocsRoot = opts.DocsRoot,
                        ConfigRoot = opts.ConfigRoot,
                        ApiKey = "",
                        VersionPattern = opts.VersionPattern,
                        BasePlaceholder = opts.BasePlaceholder,
                        MaxUploadSizeBytes = opts.MaxUploadSizeBytes,
                        PathBase = opts.PathBase
                    };
                });
            });
        });

        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_Returns401_WhenApiKeyNotConfigured()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/_api/products/disabled-test/versions/v1")
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "any-key");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReadEndpoints_StillWork_WhenUploadDisabled()
    {
        var response = await _client.GetAsync("/_api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
