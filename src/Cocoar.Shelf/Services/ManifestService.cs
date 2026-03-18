using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cocoar.FileSystem;
using Cocoar.Shelf.Models;
using Microsoft.Extensions.Options;

namespace Cocoar.Shelf.Services;

public sealed partial class ManifestService : IManifestService, IDisposable
{
    private readonly ShelfOptions _options;
    private readonly ILogger<ManifestService> _logger;
    private readonly ConcurrentDictionary<string, ProductManifest> _cache = new();
    private readonly ResilientFileSystemMonitor? _monitor;
    private readonly Regex _versionRegex;

    public ManifestService(IOptions<ShelfOptions> options, ILogger<ManifestService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _versionRegex = new Regex(_options.VersionPattern, RegexOptions.Compiled);

        if (!Directory.Exists(_options.DocsRoot))
        {
            LogDocsRootMissing(_options.DocsRoot);
            return;
        }

        _monitor = ResilientFileSystemMonitor
            .Watch(_options.DocsRoot)
            .IncludeSubdirectories(2)
            .WithDebounce(500)
            .OnCreated((_, e) => InvalidateCache(e.FullPath))
            .OnDeleted((_, e) => InvalidateCache(e.FullPath))
            .OnRenamed((_, e) =>
            {
                if (e.OldFullPath != null) InvalidateCache(e.OldFullPath);
                InvalidateCache(e.FullPath);
            })
            .OnModeChanged((_, e) =>
            {
                var mode = e.Mode.ToString();
                var reason = e.Reason ?? "";
                LogMonitorModeChanged(mode, reason);
            })
            .Build();
    }

    public ProductManifest? GetManifest(string product)
    {
        if (_cache.TryGetValue(product, out var cached))
            return cached;

        var productDir = Path.Combine(_options.DocsRoot, product);

        if (!Directory.Exists(productDir))
            return null;

        var manifest = ScanProduct(productDir);

        if (manifest == null)
            return null;

        _cache.TryAdd(product, manifest);
        return manifest;
    }

    public IReadOnlyList<string> GetProducts()
    {
        if (!Directory.Exists(_options.DocsRoot))
            return [];

        return Directory.GetDirectories(_options.DocsRoot)
            .Select(Path.GetFileName)
            .Where(name => name != null)
            .Cast<string>()
            .Order()
            .ToList();
    }

    private ProductManifest? ScanProduct(string productDir)
    {
        var versions = Directory.GetDirectories(productDir)
            .Select(Path.GetFileName)
            .Where(name => name != null && _versionRegex.IsMatch(name))
            .Cast<string>()
            .OrderByDescending(ParseVersion)
            .ToList();

        if (versions.Count == 0)
            return null;

        return new ProductManifest
        {
            Latest = versions[0],
            Versions = versions
        };
    }

    private static int ParseVersion(string version) =>
        int.TryParse(version.AsSpan(1), out var num) ? num : 0;

    private void InvalidateCache(string fullPath)
    {
        var docsRootFull = Path.GetFullPath(_options.DocsRoot);
        var changedFull = Path.GetFullPath(fullPath);

        if (!changedFull.StartsWith(docsRootFull, StringComparison.OrdinalIgnoreCase))
            return;

        var relative = Path.GetRelativePath(docsRootFull, changedFull);
        var product = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];

        if (_cache.TryRemove(product, out _))
        {
            LogCacheInvalidated(product);
        }
    }

    public void Dispose()
    {
        _monitor?.Dispose();
        GC.SuppressFinalize(this);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Docs root directory does not exist: {DocsRoot}")]
    private partial void LogDocsRootMissing(string docsRoot);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cache invalidated for product {Product}")]
    private partial void LogCacheInvalidated(string product);

    [LoggerMessage(Level = LogLevel.Information, Message = "File monitor mode changed to {Mode}: {Reason}")]
    private partial void LogMonitorModeChanged(string mode, string reason);
}
