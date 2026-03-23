using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Cocoar.Shelf.Services;

public sealed partial class BasePathDetector
{
    private readonly ConcurrentDictionary<string, string> _cache = new();

    /// <summary>
    /// Detects the base path used during the VitePress build by inspecting index.html.
    /// Looks for the first href pointing to "assets/" and extracts the prefix.
    /// </summary>
    public string Detect(string versionDir)
    {
        var key = versionDir;

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var indexPath = Path.Combine(versionDir, "index.html");

        if (!File.Exists(indexPath))
        {
            _cache.TryAdd(key, "/");
            return "/";
        }

        var html = File.ReadAllText(indexPath);

        // Look for href="...assets/" — the part before "assets/" is the base path
        var match = AssetHrefRegex().Match(html);

        var basePath = match.Success ? match.Groups[1].Value : "/";

        _cache.TryAdd(key, basePath);
        return basePath;
    }

    public void InvalidateCache(string versionDir) =>
        _cache.TryRemove(versionDir, out _);

    public void InvalidateProductCache(string productDir)
    {
        foreach (var key in _cache.Keys)
            if (key.StartsWith(productDir, StringComparison.OrdinalIgnoreCase))
                _cache.TryRemove(key, out _);
    }

    // Matches href="/some/base/assets/ and captures the base part
    [GeneratedRegex("""href="([^"]*?)assets/""")]
    private static partial Regex AssetHrefRegex();
}
