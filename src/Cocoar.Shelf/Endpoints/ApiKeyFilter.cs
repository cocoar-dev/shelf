using Microsoft.Extensions.Options;

namespace Cocoar.Shelf.Endpoints;

public class ApiKeyFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var options = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<ShelfOptions>>().Value;

        if (string.IsNullOrEmpty(options.ApiKey))
            return Results.Json(new { error = "Upload is disabled (no API key configured)" }, statusCode: 503);

        var auth = context.HttpContext.Request.Headers.Authorization.ToString();

        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Results.Json(new { error = "Missing API key" }, statusCode: 401);

        var provided = auth["Bearer ".Length..];

        if (provided != options.ApiKey)
            return Results.Json(new { error = "Invalid API key" }, statusCode: 401);

        return await next(context);
    }
}
