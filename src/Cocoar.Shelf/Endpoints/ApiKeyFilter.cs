namespace Cocoar.Shelf.Endpoints;

public partial class ApiKeyFilter(ILogger<ApiKeyFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
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
            LogMissingBearerToken(logger);
            return Results.Json(new { error = "Missing API key" }, statusCode: 401);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload rejected: missing Bearer token")]
    private static partial void LogMissingBearerToken(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload rejected: invalid API key")]
    private static partial void LogInvalidApiKey(ILogger logger);
}
