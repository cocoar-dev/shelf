using System.Text.RegularExpressions;
using Cocoar.Configuration.Reactive;
using Cocoar.Shelf.Models;
using Cocoar.Shelf.Services;
using Cocoar.Shelf.Services.Access;
using Microsoft.AspNetCore.StaticFiles;

namespace Cocoar.Shelf.Middleware;

public partial class DocsRoutingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IManifestService _manifestService;
    private readonly IProductConfigService _productConfig;
    private readonly BasePathDetector _basePathDetector;
    private readonly IReactiveConfig<ShelfOptions> _config;
    private readonly AccessLogChannel? _accessLog;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private (string Pattern, Regex Compiled) _versionRegexCache;

    public DocsRoutingMiddleware(
        RequestDelegate next,
        IManifestService manifestService,
        IProductConfigService productConfig,
        BasePathDetector basePathDetector,
        IReactiveConfig<ShelfOptions> config,
        AccessLogChannel? accessLog = null)
    {
        _next = next;
        _manifestService = manifestService;
        _productConfig = productConfig;
        _basePathDetector = basePathDetector;
        _config = config;
        _accessLog = accessLog;
        var initialPattern = config.CurrentValue.VersionPattern;
        _versionRegexCache = (initialPattern, new Regex(initialPattern, RegexOptions.Compiled));
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

        // Reserved prefixes — never interpret as product names
        if (path.StartsWith('_'))
        {
            await _next(context);
            return;
        }

        var segments = path.Split('/', 2);
        var product = segments[0];
        var productDir = Path.Combine(_config.CurrentValue.DocsRoot, product);

        if (!Directory.Exists(productDir))
        {
            await _next(context);
            return;
        }

        // Access control v2: restricted products are gated before anything is served (HTML and assets).
        var productConfig = await _productConfig.GetConfigAsync(product);
        if (productConfig?.Restricted == true && !await HasReadAccessAsync(context, productConfig))
        {
            DenyRestricted(context);
            return;
        }

        var rest = segments.Length > 1 ? segments[1] : "";
        string resolvedPath;
        string version;

        var restSegments = rest.Split('/', 2);
        // Page path WITHOUT the version segment — this is what the access log stores
        // (Path = "guide/intro", not "v1/guide/intro").
        var pagePath = restSegments.Length > 1 ? restSegments[1] : "";
        if (restSegments[0].Length > 0 && GetVersionRegex().IsMatch(restSegments[0]))
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
        var docsRootFull = Path.GetFullPath(_config.CurrentValue.DocsRoot);
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
                if (contentType.Contains("text/html"))
                    RecordAccess(context, product, version, pagePath);
                return;
            }
        }

        await context.Response.SendFileAsync(resolvedPath);

        if (contentType.Contains("text/html"))
            RecordAccess(context, product, version, pagePath);
    }

    private void RecordAccess(HttpContext context, string product, string version, string rest)
    {
        if (_accessLog == null)
            return;

        // Page views only: HEAD requests are health checks / link probes, not visits.
        if (context.Request.Method != HttpMethods.Get)
            return;

        _accessLog.Write(new AccessLogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Ip = context.Connection.RemoteIpAddress?.ToString() ?? "",
            Product = product,
            Version = version,
            Path = rest,
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            Referer = context.Request.Headers.Referer.ToString(),
            AcceptLanguage = context.Request.Headers.AcceptLanguage.ToString()
        });
    }

    private static async Task<bool> HasReadAccessAsync(HttpContext context, ProductConfig product)
    {
        var resolver = context.RequestServices.GetRequiredService<IAccessResolver>();
        var grants = await resolver.ResolveAsync(context.User);
        return grants.CanRead(product);
    }

    // Restricted product, no read grant. Decided (concept): authenticated-but-unauthorized → 404 (no
    // existence leak); anonymous HTML navigation → login redirect (silent SSO makes it painless);
    // anonymous asset fetch → 401.
    private static void DenyRestricted(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            context.Response.StatusCode = 404;
            return;
        }

        if (WantsHtml(context.Request))
        {
            var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            context.Response.Redirect($"{context.Request.PathBase}/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }
        else
        {
            context.Response.StatusCode = 401;
        }
    }

    // Page navigations send Accept: text/html; asset fetches (js/css/img) don't. Fall back to the path
    // extension so an extensionless page path still redirects to login rather than returning 401.
    private static bool WantsHtml(HttpRequest request)
    {
        if (request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
            return true;
        var ext = Path.GetExtension(request.Path.Value ?? "");
        return string.IsNullOrEmpty(ext) || ext.Equals(".html", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTextContent(string contentType) =>
        contentType.Contains("text/html") ||
        contentType.Contains("text/css") ||
        contentType.Contains("application/javascript") ||
        contentType.Contains("text/javascript");

    private Regex GetVersionRegex()
    {
        var pattern = _config.CurrentValue.VersionPattern;
        if (pattern == _versionRegexCache.Pattern) return _versionRegexCache.Compiled;
        var compiled = new Regex(pattern, RegexOptions.Compiled);
        _versionRegexCache = (pattern, compiled);
        return compiled;
    }

    [GeneratedRegex(@"\.[a-f0-9]{6,}\.(css|js)$")]
    private static partial Regex HashedAssetRegex();
}
