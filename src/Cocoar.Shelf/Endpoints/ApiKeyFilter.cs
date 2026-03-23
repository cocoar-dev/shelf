namespace Cocoar.Shelf.Endpoints;

/// <summary>
/// Endpoint filter that accepts authentication via either:
/// - Cookie session (from Admin UI / browser)
/// - Bearer API key (from CI/CD pipelines)
/// </summary>
public partial class ApiKeyFilter(ILogger<ApiKeyFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Accept cookie-based authentication (Admin UI)
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
            return await next(context);

        // Accept Bearer API key (CI/CD)
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

        if (string.IsNullOrEmpty(options.ApiKey))
        {
            LogNoApiKeyConfigured(logger);
            return Results.Json(new { error = "Upload is disabled (no API key configured)" }, statusCode: 503);
        }

        var auth = context.HttpContext.Request.Headers.Authorization.ToString();

        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            LogUnauthorized(logger);
            return Results.Json(new { error = "Authentication required" }, statusCode: 401);
        }

        var provided = auth["Bearer ".Length..];

        if (provided != options.ApiKey)
        {
            LogInvalidApiKey(logger);
            return Results.Json(new { error = "Invalid API key" }, statusCode: 401);
        }

        return await next(context);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to resolve ShelfOptions — configuration may be invalid")]
    private static partial void LogConfigResolutionFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload rejected: no API key configured")]
    private static partial void LogNoApiKeyConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authentication required: no cookie or Bearer token")]
    private static partial void LogUnauthorized(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload rejected: invalid API key")]
    private static partial void LogInvalidApiKey(ILogger logger);
}
