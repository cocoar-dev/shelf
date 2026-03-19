using System.Text.RegularExpressions;
using Cocoar.Shelf.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace Cocoar.Shelf.Endpoints;

public static class ApiEndpoints
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/products", GetProducts);
        api.MapGet("/products/{product}/versions", GetVersions);
        api.MapPost("/products/{product}/versions/{version}", UploadVersion)
            .AddEndpointFilter<ApiKeyFilter>();

        return app;
    }

    private static IResult GetProducts(IProductConfigService configService, IManifestService manifestService)
    {
        var products = configService.GetAll().Select(config =>
        {
            var manifest = manifestService.GetManifest(config.Name);
            return new
            {
                config.Name,
                config.DisplayName,
                config.Description,
                config.Source,
                Latest = manifest?.Latest,
                Versions = manifest?.Versions ?? (IReadOnlyList<string>)[]
            };
        });

        return Results.Ok(products);
    }

    private static IResult GetVersions(
        string product,
        IProductConfigService configService,
        IManifestService manifestService)
    {
        var config = configService.GetConfig(product);
        if (config == null)
            return Results.Json(new { error = $"Product '{product}' is not registered" }, statusCode: 404);

        var manifest = manifestService.GetManifest(product);

        return Results.Ok(new
        {
            Name = product,
            Latest = manifest?.Latest,
            Versions = manifest?.Versions ?? (IReadOnlyList<string>)[]
        });
    }

    private static async Task<IResult> UploadVersion(
        string product,
        string version,
        HttpContext httpContext,
        IProductConfigService configService,
        IUploadService uploadService,
        IOptions<ShelfOptions> options,
        CancellationToken ct)
    {
        var opts = options.Value;

        // Increase request body size limit for this endpoint
        var maxSizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxSizeFeature is { IsReadOnly: false })
            maxSizeFeature.MaxRequestBodySize = opts.MaxUploadSizeBytes;

        // Check product is registered
        var config = configService.GetConfig(product);
        if (config == null)
            return Results.Json(new { error = $"Product '{product}' is not registered" }, statusCode: 404);

        // Validate version format
        if (!Regex.IsMatch(version, opts.VersionPattern))
            return Results.Json(new { error = $"Invalid version format: '{version}'" }, statusCode: 400);

        // Check Content-Length if present
        if (httpContext.Request.ContentLength > opts.MaxUploadSizeBytes)
            return Results.Json(new { error = "Upload exceeds maximum allowed size" }, statusCode: 413);

        // Read body with size limit
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await httpContext.Request.Body.ReadAsync(buffer, ct)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > opts.MaxUploadSizeBytes)
                return Results.Json(new { error = "Upload exceeds maximum allowed size" }, statusCode: 413);
            ms.Write(buffer, 0, bytesRead);
        }

        ms.Position = 0;

        var result = await uploadService.UploadVersionAsync(product, version, ms, ct);

        return result.Status switch
        {
            UploadStatus.Success => Results.Created($"{httpContext.Request.PathBase}/api/products/{product}/versions/{version}", null),
            UploadStatus.MissingIndexHtml => Results.Json(
                new { error = result.Error ?? "Archive must contain an index.html at the root" }, statusCode: 400),
            UploadStatus.VersionConflict => Results.Json(
                new { error = result.Error ?? "Upload for this version is already in progress" }, statusCode: 409),
            UploadStatus.InvalidArchive => Results.Json(
                new { error = result.Error ?? "Invalid ZIP archive" }, statusCode: 400),
            _ => Results.Json(new { error = "Internal error" }, statusCode: 500)
        };
    }
}
