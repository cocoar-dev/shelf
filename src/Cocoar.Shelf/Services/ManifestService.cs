using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cocoar.Configuration.Reactive;
using Cocoar.FileSystem;
using Cocoar.Shelf.Models;

namespace Cocoar.Shelf.Services;

public sealed partial class ManifestService : IManifestService, IDisposable
{
    private readonly IReactiveConfig<ShelfOptions> _config;
    private readonly ILogger<ManifestService> _logger;
    private readonly ConcurrentDictionary<string, ProductManifest> _cache = new();
    private readonly ResilientFileSystemMonitor? _monitor;
    private readonly Regex _versionRegex;

    public ManifestService(IReactiveConfig<ShelfOptions> config, ILogger<ManifestService> logger)
    {
        _config = config;
        _logger = logger;
        _versionRegex = new Regex(_config.CurrentValue.VersionPattern, RegexOptions.Compiled);

        if (!Directory.Exists(_config.CurrentValue.DocsRoot))
        {
            LogDocsRootMissing(_config.CurrentValue.DocsRoot);
            return;
        }

        _monitor = ResilientFileSystemMonitor
            .Watch(_config.CurrentValue.DocsRoot)
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

        var productDir = Path.Combine(_config.CurrentValue.DocsRoot, product);

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
        if (!Directory.Exists(_config.CurrentValue.DocsRoot))
            return [];

        return Directory.GetDirectories(_config.CurrentValue.DocsRoot)
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

        // Latest = highest stable version, fallback to highest pre-release
        var latest = versions.FirstOrDefault(v => ParseVersion(v).IsStable) ?? versions[0];

        return new ProductManifest
        {
            Latest = latest,
            Versions = versions
        };
    }

    internal static (int Major, int Minor, int Patch, bool IsStable, string Pre) ParseVersion(string version)
    {
        var s = version.AsSpan();
        if (s.Length > 0 && s[0] is 'v' or 'V')
            s = s[1..];

        var dashIndex = s.IndexOf('-');
        string pre = "";
        if (dashIndex >= 0)
        {
            pre = s[(dashIndex + 1)..].ToString();
            s = s[..dashIndex];
        }

        var parts = s.ToString().Split('.');
        var major = parts.Length > 0 && int.TryParse(parts[0], out var ma) ? ma : 0;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var mi) ? mi : 0;
        var patch = parts.Length > 2 && int.TryParse(parts[2], out var pa) ? pa : 0;

        return (major, minor, patch, pre.Length == 0, pre);
    }

    private void InvalidateCache(string fullPath)
    {
        var docsRootFull = Path.GetFullPath(_config.CurrentValue.DocsRoot);
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
