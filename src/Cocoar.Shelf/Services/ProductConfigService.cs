using System.Collections.Concurrent;
using System.Text.Json;
using Cocoar.FileSystem;
using Cocoar.Shelf.Models;
using Microsoft.Extensions.Options;

namespace Cocoar.Shelf.Services;

public sealed partial class ProductConfigService : IProductConfigService, IDisposable
{
    private readonly string _productsDir;
    private readonly ILogger<ProductConfigService> _logger;
    private readonly ConcurrentDictionary<string, ProductConfig> _cache = new();
    private readonly ResilientFileSystemMonitor? _monitor;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public ProductConfigService(IOptions<ShelfOptions> options, ILogger<ProductConfigService> logger)
    {
        _logger = logger;
        _productsDir = Path.Combine(options.Value.ConfigRoot, "products");

        if (!Directory.Exists(_productsDir))
        {
            LogConfigDirMissing(_productsDir);
            return;
        }

        LoadAll();

        _monitor = ResilientFileSystemMonitor
            .Watch(_productsDir)
            .IncludeSubdirectories(0)
            .WithDebounce(500)
            .OnCreated((_, e) => ReloadFile(e.FullPath))
            .OnDeleted((_, e) => RemoveByFile(e.FullPath))
            .OnRenamed((_, e) =>
            {
                if (e.OldFullPath != null) RemoveByFile(e.OldFullPath);
                ReloadFile(e.FullPath);
            })
            .OnModeChanged((_, e) =>
            {
                var mode = e.Mode.ToString();
                var reason = e.Reason ?? "";
                LogMonitorModeChanged(mode, reason);
            })
            .Build();
    }

    public ProductConfig? GetConfig(string name)
    {
        _cache.TryGetValue(name, out var config);
        return config;
    }

    public IReadOnlyList<ProductConfig> GetAll()
    {
        return _cache.Values.OrderBy(c => c.Name).ToList();
    }

    private void LoadAll()
    {
        foreach (var file in Directory.GetFiles(_productsDir, "*.json"))
        {
            TryLoadFile(file);
        }
    }

    private void ReloadFile(string fullPath)
    {
        if (!fullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return;

        TryLoadFile(fullPath);
    }

    private void RemoveByFile(string fullPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(fullPath);
        if (fileName == null) return;

        // The cache key is the product name from the JSON, not the filename.
        // But since convention is filename == product name, try removing by filename first.
        // Also scan cache for any config loaded from this file.
        var toRemove = _cache.Where(kvp =>
            string.Equals(kvp.Key, fileName, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            if (_cache.TryRemove(key, out _))
                LogConfigRemoved(key);
        }
    }

    private void TryLoadFile(string fullPath)
    {
        try
        {
            var json = File.ReadAllText(fullPath);
            var config = JsonSerializer.Deserialize<ProductConfig>(json, JsonOptions);

            if (config == null)
            {
                LogConfigInvalid(fullPath, "deserialized to null");
                return;
            }

            _cache[config.Name] = config;
            LogConfigLoaded(config.Name);
        }
        catch (Exception ex)
        {
            LogConfigInvalid(fullPath, ex.Message);
        }
    }

    public void Dispose()
    {
        _monitor?.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Product config directory does not exist: {Path}")]
    private partial void LogConfigDirMissing(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Product config loaded: {Name}")]
    private partial void LogConfigLoaded(string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Product config removed: {Name}")]
    private partial void LogConfigRemoved(string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid product config {Path}: {Error}")]
    private partial void LogConfigInvalid(string path, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Config monitor mode changed to {Mode}: {Reason}")]
    private partial void LogMonitorModeChanged(string mode, string reason);
}
