using Cocoar.Shelf.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cocoar.Shelf.Tests;

public sealed class ApiKeyFilterTests
{
    [Fact]
    public async Task Returns503_WhenApiKeyIsEmpty()
    {
        var result = await InvokeFilter(apiKey: "", authHeader: null);
        var statusCode = await GetStatusCode(result);

        Assert.Equal(503, statusCode);
    }

    [Fact]
    public async Task Returns401_WhenNoAuthorizationHeader()
    {
        var result = await InvokeFilter(apiKey: "my-key", authHeader: null);
        var statusCode = await GetStatusCode(result);

        Assert.Equal(401, statusCode);
    }

    [Fact]
    public async Task Returns401_WhenAuthHeaderHasNoBearerPrefix()
    {
        var result = await InvokeFilter(apiKey: "my-key", authHeader: "Basic abc123");
        var statusCode = await GetStatusCode(result);

        Assert.Equal(401, statusCode);
    }

    [Fact]
    public async Task Returns401_WhenKeyDoesNotMatch()
    {
        var result = await InvokeFilter(apiKey: "my-key", authHeader: "Bearer wrong-key");
        var statusCode = await GetStatusCode(result);

        Assert.Equal(401, statusCode);
    }

    [Fact]
    public async Task PassesThrough_WhenKeyMatches()
    {
        var result = await InvokeFilter(apiKey: "my-key", authHeader: "Bearer my-key");

        Assert.Equal("passed", result);
    }

    private static async Task<object?> InvokeFilter(string apiKey, string? authHeader)
    {
        var services = new ServiceCollection();
        services.Configure<ShelfOptions>(o => o.ApiKey = apiKey);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        if (authHeader != null)
            httpContext.Request.Headers.Authorization = authHeader;

        var context = new DefaultEndpointFilterInvocationContext(httpContext);
        var filter = new ApiKeyFilter();

        return await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("passed"));
    }

    private static async Task<int> GetStatusCode(object? result)
    {
        if (result is IResult httpResult)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(_ => { });

            var httpContext = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider()
            };
            httpContext.Response.Body = new MemoryStream();
            await httpResult.ExecuteAsync(httpContext);
            return httpContext.Response.StatusCode;
        }
        return -1;
    }
}
