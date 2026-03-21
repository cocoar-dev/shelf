using System.Text.RegularExpressions;
using Cocoar.Shelf.Models;
using Cocoar.Shelf.Services;
using Microsoft.AspNetCore.Http.Features;

namespace Cocoar.Shelf.Endpoints;

public static partial class ApiEndpoints
{
    private static readonly Regex ProductNameRegex = new("^[a-z0-9][a-z0-9-]*$", RegexOptions.Compiled);
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase) { "admin", "api" };

    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/_api");

        // Auth endpoints (cookie-based)
        api.MapAuthEndpoints();

        // Public read endpoints
        api.MapGet("/products", GetProducts);
        api.MapGet("/products/{product}/versions", GetVersions);

        // Protected write endpoints (cookie or Bearer API key)
        api.MapPost("/products", CreateProduct)
            .AddEndpointFilter<ApiKeyFilter>();
        api.MapPut("/products/{product}", UpdateProduct)
            .AddEndpointFilter<ApiKeyFilter>();
        api.MapDelete("/products/{product}", DeleteProduct)
            .AddEndpointFilter<ApiKeyFilter>();
        api.MapPost("/products/{product}/versions/{version}", UploadVersion)
            .AddEndpointFilter<ApiKeyFilter>();
        api.MapDelete("/products/{product}/versions/{version}", DeleteVersion)
            .AddEndpointFilter<ApiKeyFilter>();

        return app;
    }

    private static IResult GetProducts(
        IProductConfigService configService,
        IManifestService manifestService,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Api");
        try
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
                    config.Visibility,
                    Latest = manifest?.Latest,
                    Versions = manifest?.Versions ?? (IReadOnlyList<string>)[]
                };
            });

            return Results.Ok(products);
        }
        catch (Exception ex)
        {
            LogListProductsFailed(logger, ex);
            return Results.Json(new { error = "Failed to list products" }, statusCode: 500);
        }
    }

    private static IResult GetVersions(
        string product,
        IProductConfigService configService,
        IManifestService manifestService,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Api");
        try
        {
            var config = configService.GetConfig(product);
            if (config == null)
            {
                LogProductNotRegistered(logger, product);
                return Results.Json(new { error = $"Product '{product}' is not registered" }, statusCode: 404);
            }

            var manifest = manifestService.GetManifest(product);

            return Results.Ok(new
            {
                Name = product,
                Latest = manifest?.Latest,
                Versions = manifest?.Versions ?? (IReadOnlyList<string>)[]
            });
        }
        catch (Exception ex)
        {
            LogGetVersionsFailed(logger, product, ex);
            return Results.Json(new { error = $"Failed to get versions for product '{product}'" }, statusCode: 500);
        }
    }

    private static async Task<IResult> UploadVersion(
        string product,
        string version,
        HttpContext httpContext,
        IProductConfigService configService,
        IUploadService uploadService,
        ShelfOptions options,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Api");
        try
        {
            return await UploadVersionCore(product, version, httpContext, configService, uploadService, options, logger, ct);
        }
        catch (BadHttpRequestException ex)
        {
            LogUploadBadRequest(logger, product, version, ex);
            return Results.Json(new { error = $"Bad request: {ex.Message}" }, statusCode: 400);
        }
        catch (OperationCanceledException)
        {
            LogUploadCancelled(logger, product, version);
            return Results.Json(new { error = "Upload cancelled" }, statusCode: 499);
        }
        catch (Exception ex)
        {
            LogUploadFailed(logger, product, version, ex);
            return Results.Json(new { error = $"Upload failed: {ex.Message}" }, statusCode: 500);
        }
    }

    private static async Task<IResult> UploadVersionCore(
        string product,
        string version,
        HttpContext httpContext,
        IProductConfigService configService,
        IUploadService uploadService,
        ShelfOptions opts,
        ILogger logger,
        CancellationToken ct)
    {
        // Increase request body size limit for this endpoint
        var maxSizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxSizeFeature is { IsReadOnly: false })
            maxSizeFeature.MaxRequestBodySize = opts.MaxUploadSizeBytes;

        // Check product is registered
        var config = configService.GetConfig(product);
        if (config == null)
        {
            LogUploadProductNotRegistered(logger, product);
            return Results.Json(new { error = $"Product '{product}' is not registered" }, statusCode: 404);
        }

        // Validate version format
        if (string.IsNullOrWhiteSpace(version))
        {
            LogUploadEmptyVersion(logger, product);
            return Results.Json(new { error = "Version must not be empty" }, statusCode: 400);
        }

        if (!Regex.IsMatch(version, opts.VersionPattern))
        {
            LogUploadInvalidVersion(logger, version, product, opts.VersionPattern);
            return Results.Json(
                new { error = $"Invalid version format: '{version}'. Must match pattern: {opts.VersionPattern}" },
                statusCode: 400);
        }

        // Check Content-Length if present
        if (httpContext.Request.ContentLength > opts.MaxUploadSizeBytes)
        {
            LogUploadTooLarge(logger, httpContext.Request.ContentLength, opts.MaxUploadSizeBytes, product, version);
            return Results.Json(new { error = $"Upload exceeds maximum allowed size ({opts.MaxUploadSizeBytes} bytes)" }, statusCode: 413);
        }

        // Read body with size limit
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await httpContext.Request.Body.ReadAsync(buffer, ct)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > opts.MaxUploadSizeBytes)
            {
                LogUploadBodyTooLarge(logger, opts.MaxUploadSizeBytes, product, version);
                return Results.Json(new { error = $"Upload exceeds maximum allowed size ({opts.MaxUploadSizeBytes} bytes)" }, statusCode: 413);
            }
            ms.Write(buffer, 0, bytesRead);
        }

        ms.Position = 0;

        LogUploadProcessing(logger, product, version, totalRead);

        var result = await uploadService.UploadVersionAsync(product, version, ms, ct);

        switch (result.Status)
        {
            case UploadStatus.Success:
                return Results.Created($"{httpContext.Request.PathBase}/_api/products/{product}/versions/{version}", null);

            case UploadStatus.MissingIndexHtml:
                LogUploadRejected(logger, product, version, "missing index.html");
                return Results.Json(new { error = result.Error ?? "Archive must contain an index.html at the root" }, statusCode: 400);

            case UploadStatus.VersionConflict:
                LogUploadRejected(logger, product, version, "concurrent upload");
                return Results.Json(new { error = result.Error ?? "Upload for this version is already in progress" }, statusCode: 409);

            case UploadStatus.InvalidArchive:
                LogUploadRejected(logger, product, version, result.Error ?? "invalid archive");
                return Results.Json(new { error = result.Error ?? "Invalid ZIP archive" }, statusCode: 400);

            default:
                LogUploadUnexpectedStatus(logger, product, version, result.Status);
                return Results.Json(new { error = result.Error ?? "Internal error" }, statusCode: 500);
        }
    }

    private static async Task<IResult> CreateProduct(
        CreateProductRequest request,
        IProductConfigService configService,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Api");
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.Json(new { error = "Product name is required" }, statusCode: 400);

            if (!ProductNameRegex.IsMatch(request.Name))
                return Results.Json(new { error = "Product name must contain only lowercase letters, numbers, and hyphens" }, statusCode: 400);

            if (ReservedNames.Contains(request.Name))
                return Results.Json(new { error = $"'{request.Name}' is a reserved name" }, statusCode: 400);

            if (configService.GetConfig(request.Name) != null)
            {
                LogProductAlreadyExists(logger, request.Name);
                return Results.Json(new { error = $"Product '{request.Name}' already exists" }, statusCode: 409);
            }

            var config = new ProductConfig
            {
                Name = request.Name,
                DisplayName = request.DisplayName,
                Description = request.Description,
                Source = request.Source ?? "upload",
                Visibility = request.Visibility ?? "public"
            };

            await configService.CreateAsync(config);
            LogProductCreated(logger, request.Name);
            return Results.Created($"/_api/products/{request.Name}", config);
        }
        catch (Exception ex)
        {
            LogProductOperationFailed(logger, "create", request.Name, ex);
            return Results.Json(new { error = $"Failed to create product: {ex.Message}" }, statusCode: 500);
        }
    }

    private static async Task<IResult> UpdateProduct(
        string product,
        UpdateProductRequest request,
        IProductConfigService configService,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Api");
        try
        {
            var existing = configService.GetConfig(product);
            if (existing == null)
            {
                LogProductNotRegistered(logger, product);
                return Results.Json(new { error = $"Product '{product}' is not registered" }, statusCode: 404);
            }

            var config = new ProductConfig
            {
                Name = product,
                DisplayName = request.DisplayName ?? existing.DisplayName,
                Description = request.Description ?? existing.Description,
                Source = request.Source ?? existing.Source,
                Visibility = request.Visibility ?? existing.Visibility
            };

            await configService.UpdateAsync(config);
            LogProductUpdated(logger, product);
            return Results.Ok(config);
        }
        catch (Exception ex)
        {
            LogProductOperationFailed(logger, "update", product, ex);
            return Results.Json(new { error = $"Failed to update product: {ex.Message}" }, statusCode: 500);
        }
    }

    private static async Task<IResult> DeleteProduct(
        string product,
        IProductConfigService configService,
        IUploadService uploadService,
        ILoggerFactory loggerFactory,
        bool deleteData = false)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Api");
        try
        {
            var existing = configService.GetConfig(product);
            if (existing == null)
            {
                LogProductNotRegistered(logger, product);
                return Results.Json(new { error = $"Product '{product}' is not registered" }, statusCode: 404);
            }

            await configService.DeleteAsync(product);
            LogProductDeleted(logger, product, deleteData);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            LogProductOperationFailed(logger, "delete", product, ex);
            return Results.Json(new { error = $"Failed to delete product: {ex.Message}" }, statusCode: 500);
        }
    }

    private static async Task<IResult> DeleteVersion(
        string product,
        string version,
        IProductConfigService configService,
        IUploadService uploadService,
        ShelfOptions options,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Api");
        try
        {
            var config = configService.GetConfig(product);
            if (config == null)
            {
                LogProductNotRegistered(logger, product);
                return Results.Json(new { error = $"Product '{product}' is not registered" }, statusCode: 404);
            }

            if (!Regex.IsMatch(version, options.VersionPattern))
            {
                LogUploadInvalidVersion(logger, version, product, options.VersionPattern);
                return Results.Json(new { error = $"Invalid version format: '{version}'" }, statusCode: 400);
            }

            var deleted = await uploadService.DeleteVersionAsync(product, version, ct);
            if (!deleted)
                return Results.Json(new { error = $"Version '{version}' not found for product '{product}'" }, statusCode: 404);

            LogVersionDeleted(logger, product, version);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            LogProductOperationFailed(logger, "delete version", $"{product}/{version}", ex);
            return Results.Json(new { error = $"Failed to delete version: {ex.Message}" }, statusCode: 500);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to list products")]
    private static partial void LogListProductsFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Product not registered: {Product}")]
    private static partial void LogProductNotRegistered(ILogger logger, string product);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get versions for product {Product}")]
    private static partial void LogGetVersionsFailed(ILogger logger, string product, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload bad request for {Product}/{Version}")]
    private static partial void LogUploadBadRequest(ILogger logger, string product, string version, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Upload cancelled for {Product}/{Version}")]
    private static partial void LogUploadCancelled(ILogger logger, string product, string version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Upload failed for {Product}/{Version}")]
    private static partial void LogUploadFailed(ILogger logger, string product, string version, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload rejected: product {Product} is not registered")]
    private static partial void LogUploadProductNotRegistered(ILogger logger, string product);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload rejected: empty version for product {Product}")]
    private static partial void LogUploadEmptyVersion(ILogger logger, string product);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload rejected: invalid version format {Version} for product {Product} (pattern: {Pattern})")]
    private static partial void LogUploadInvalidVersion(ILogger logger, string version, string product, string pattern);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload rejected: content length {Length} exceeds limit {Limit} for {Product}/{Version}")]
    private static partial void LogUploadTooLarge(ILogger logger, long? length, long limit, string product, string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload rejected: body exceeds limit {Limit} for {Product}/{Version}")]
    private static partial void LogUploadBodyTooLarge(ILogger logger, long limit, string product, string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing upload for {Product}/{Version} ({Bytes} bytes)")]
    private static partial void LogUploadProcessing(ILogger logger, string product, string version, long bytes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload rejected for {Product}/{Version}: {Reason}")]
    private static partial void LogUploadRejected(ILogger logger, string product, string version, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Upload failed with unexpected status {Status} for {Product}/{Version}")]
    private static partial void LogUploadUnexpectedStatus(ILogger logger, string product, string version, UploadStatus status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Product already exists: {Product}")]
    private static partial void LogProductAlreadyExists(ILogger logger, string product);

    [LoggerMessage(Level = LogLevel.Information, Message = "Product created: {Product}")]
    private static partial void LogProductCreated(ILogger logger, string product);

    [LoggerMessage(Level = LogLevel.Information, Message = "Product updated: {Product}")]
    private static partial void LogProductUpdated(ILogger logger, string product);

    [LoggerMessage(Level = LogLevel.Information, Message = "Product deleted: {Product} (deleteData={DeleteData})")]
    private static partial void LogProductDeleted(ILogger logger, string product, bool deleteData);

    [LoggerMessage(Level = LogLevel.Information, Message = "Version deleted: {Product}/{Version}")]
    private static partial void LogVersionDeleted(ILogger logger, string product, string version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to {Operation} product {Product}")]
    private static partial void LogProductOperationFailed(ILogger logger, string operation, string product, Exception ex);
}
