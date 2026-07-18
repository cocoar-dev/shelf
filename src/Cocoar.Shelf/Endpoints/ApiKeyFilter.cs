using Cocoar.Shelf.Services;

namespace Cocoar.Shelf.Endpoints;

/// <summary>
/// Endpoint filter that accepts authentication via either:
/// - Cookie session (from Admin UI / browser)
/// - Bearer API key: per-product key (from ProductConfig.ApiKey) or global key (from ShelfOptions.ApiKey)
/// </summary>
public partial class ApiKeyFilter(ILogger<ApiKeyFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Accept cookie-based authentication (Admin UI)
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
            return await next(context);

        // Extract Bearer token
        var auth = context.HttpContext.Request.Headers.Authorization.ToString();

        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            LogUnauthorized(logger);
            return Results.Json(new { error = "Authentication required" }, statusCode: 401);
        }

        var provided = auth["Bearer ".Length..].Trim();

        if (string.IsNullOrEmpty(provided))
        {
            LogUnauthorized(logger);
            return Results.Json(new { error = "Authentication required" }, statusCode: 401);
        }

        // Check per-product API key (if request targets a specific product)
        var product = context.HttpContext.Request.RouteValues["product"] as string;
        if (!string.IsNullOrEmpty(product))
        {
            var configService = context.HttpContext.RequestServices.GetService<IProductConfigService>();
            var productConfig = configService == null ? null : await configService.GetConfigAsync(product);

            if (productConfig?.ApiKey != null && productConfig.ApiKey == provided)
                return await next(context);
        }

        // UI-managed master key (settings document)
        var settings = context.HttpContext.RequestServices.GetService<ISettingsService>();
        var masterKey = settings?.Current.MasterApiKey;
        if (!string.IsNullOrEmpty(masterKey) && provided == masterKey)
            return await next(context);

        // Fallback: the env/config bootstrap key
        ShelfOptions options;
        try
        {
            options = context.HttpContext.RequestServices.GetRequiredService<ShelfOptions>();
        }
        catch (Exception ex)
        {
            LogConfigResolutionFailed(logger, ex);
            return Results.Json(new { error = "Server configuration error" }, statusCode: 500);
        }

        if (!string.IsNullOrEmpty(options.ApiKey) && provided == options.ApiKey)
            return await next(context);

        LogInvalidApiKey(logger);
        return Results.Json(new { error = "Invalid API key" }, statusCode: 401);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to resolve ShelfOptions — configuration may be invalid")]
    private static partial void LogConfigResolutionFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authentication required: no cookie or Bearer token")]
    private static partial void LogUnauthorized(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid API key")]
    private static partial void LogInvalidApiKey(ILogger logger);
}
