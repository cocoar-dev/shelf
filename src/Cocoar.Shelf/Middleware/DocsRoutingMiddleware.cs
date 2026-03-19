using System.Text.RegularExpressions;
using Cocoar.Shelf.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace Cocoar.Shelf.Middleware;

public partial class DocsRoutingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IManifestService _manifestService;
    private readonly BasePathDetector _basePathDetector;
    private readonly ShelfOptions _options;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private readonly Regex _versionRegex;

    public DocsRoutingMiddleware(
        RequestDelegate next,
        IManifestService manifestService,
        BasePathDetector basePathDetector,
        IOptions<ShelfOptions> options)
    {
        _next = next;
        _manifestService = manifestService;
        _basePathDetector = basePathDetector;
        _options = options.Value;
        _versionRegex = new Regex(_options.VersionPattern, RegexOptions.Compiled);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Get && context.Request.Method != HttpMethods.Head)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value?.Trim('/') ?? "";

        if (string.IsNullOrEmpty(path))
        {
            await _next(context);
            return;
        }

        var segments = path.Split('/', 2);
        var product = segments[0];
        var productDir = Path.Combine(_options.DocsRoot, product);

        if (!Directory.Exists(productDir))
        {
            await _next(context);
            return;
        }

        var rest = segments.Length > 1 ? segments[1] : "";
        string resolvedPath;
        string version;

        var restSegments = rest.Split('/', 2);
        if (restSegments[0].Length > 0 && _versionRegex.IsMatch(restSegments[0]))
        {
            version = restSegments[0];
            resolvedPath = Path.Combine(productDir, rest);
        }
        else
        {
            // No explicit version — redirect to latest so the URL matches
            // the VitePress base path and client-side routing works correctly
            var manifest = _manifestService.GetManifest(product);

            if (manifest == null)
            {
                context.Response.StatusCode = 404;
                return;
            }

            var redirectPath = $"{context.Request.PathBase}/{product}/{manifest.Latest}/{rest}";
            context.Response.Redirect(redirectPath, permanent: false);
            return;
        }

        // Directory requests → serve index.html
        // Note: Directory.Exists handles SemVer directories like "v0.1" where
        // Path.GetExtension would incorrectly treat ".1" as a file extension
        if (string.IsNullOrEmpty(Path.GetExtension(resolvedPath)) || Directory.Exists(resolvedPath))
        {
            resolvedPath = Path.Combine(resolvedPath, "index.html");
        }

        resolvedPath = Path.GetFullPath(resolvedPath);

        // Security: prevent path traversal outside docs root
        var docsRootFull = Path.GetFullPath(_options.DocsRoot);
        if (!resolvedPath.StartsWith(docsRootFull, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 400;
            return;
        }

        if (!File.Exists(resolvedPath))
        {
            context.Response.StatusCode = 404;
            return;
        }

        if (!_contentTypeProvider.TryGetContentType(resolvedPath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        // Immutable caching for hashed assets (e.g. style.a1b2c3d4.css)
        if (HashedAssetRegex().IsMatch(Path.GetFileName(resolvedPath)))
        {
            context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }

        context.Response.ContentType = contentType;

        // Rewrite base path in text-based responses
        if (IsTextContent(contentType))
        {
            var versionDir = Path.Combine(productDir, version);
            var originalBase = _basePathDetector.Detect(versionDir);
            var targetBase = $"{context.Request.PathBase}/{product}/{version}/";

            if (originalBase != targetBase)
            {
                var content = await File.ReadAllTextAsync(resolvedPath);
                var rewritten = BasePathRewriter.Rewrite(content, originalBase, targetBase, contentType);
                await context.Response.WriteAsync(rewritten);
                return;
            }
        }

        await context.Response.SendFileAsync(resolvedPath);
    }

    private static bool IsTextContent(string contentType) =>
        contentType.Contains("text/html") ||
        contentType.Contains("text/css") ||
        contentType.Contains("application/javascript") ||
        contentType.Contains("text/javascript");

    [GeneratedRegex(@"\.[a-f0-9]{6,}\.(css|js)$")]
    private static partial Regex HashedAssetRegex();
}
