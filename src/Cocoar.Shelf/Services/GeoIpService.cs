using System.Globalization;
using System.IO.Compression;
using System.Net;
using Cocoar.Configuration.Reactive;
using MaxMind.GeoIP2;

namespace Cocoar.Shelf.Services;

public sealed partial class GeoIpService : IDisposable
{
    private readonly IReactiveConfig<ShelfOptions> _config;
    private readonly HttpClient _http;
    private readonly ILogger<GeoIpService> _logger;
    private readonly object _lock = new();
    private DatabaseReader? _reader;
    private DateTimeOffset? _lastUpdated;

    public GeoIpService(
        IReactiveConfig<ShelfOptions> config,
        IHttpClientFactory httpClientFactory,
        ILogger<GeoIpService> logger)
    {
        _config = config;
        _http = httpClientFactory.CreateClient("geoip");
        _logger = logger;

        TryLoadExisting();
    }

    public (string? CountryCode, string? Country, string? City, string? Region)? Lookup(string ip)
    {
        if (string.IsNullOrEmpty(ip) || !IPAddress.TryParse(ip, out var addr))
            return null;

        if (IPAddress.IsLoopback(addr) || addr.Equals(IPAddress.IPv6Loopback) || IsPrivate(addr))
            return null;

        DatabaseReader? reader;
        lock (_lock) { reader = _reader; }

        if (reader == null)
            return null;

        try
        {
            if (reader.TryCity(addr, out var response))
            {
                return (
                    response?.Country?.IsoCode,
                    response?.Country?.Name,
                    response?.City?.Name,
                    response?.MostSpecificSubdivision?.Name
                );
            }
        }
        catch (Exception ex)
        {
            LogLookupFailed(_logger, ip, ex);
        }

        return null;
    }

    public DateTimeOffset? LastUpdated => _lastUpdated;

    public bool IsLoaded
    {
        get { lock (_lock) { return _reader != null; } }
    }

    public async Task<(bool Success, string? Error)> DownloadAsync(CancellationToken ct = default)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "https://download.db-ip.com/free/dbip-city-lite-{0:yyyy}-{0:MM}.mmdb.gz",
                now);

            LogDownloadStarted(_logger, url);

            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return (false, $"Download failed: HTTP {(int)response.StatusCode}");

            var dbPath = GetDbPath();
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            var tempPath = dbPath + ".tmp";
            await using (var gzStream = await response.Content.ReadAsStreamAsync(ct))
            await using (var decompressed = new GZipStream(gzStream, CompressionMode.Decompress))
            await using (var fileStream = File.Create(tempPath))
            {
                await decompressed.CopyToAsync(fileStream, ct);
            }

            // Atomic replace
            if (File.Exists(dbPath))
                File.Delete(dbPath);
            File.Move(tempPath, dbPath);

            // Reload
            LoadFromFile(dbPath);
            _lastUpdated = now;

            var size = new FileInfo(dbPath).Length;
            LogDownloadCompleted(_logger, size);

            return (true, null);
        }
        catch (Exception ex)
        {
            LogDownloadFailed(_logger, ex);
            return (false, ex.Message);
        }
    }

    private void TryLoadExisting()
    {
        var dbPath = GetDbPath();
        if (!File.Exists(dbPath))
            return;

        try
        {
            LoadFromFile(dbPath);
            _lastUpdated = new FileInfo(dbPath).LastWriteTimeUtc;
            LogDbLoaded(_logger, dbPath);
        }
        catch (Exception ex)
        {
            LogDbLoadFailed(_logger, dbPath, ex);
        }
    }

    private void LoadFromFile(string path)
    {
        var newReader = new DatabaseReader(path);

        DatabaseReader? oldReader;
        lock (_lock)
        {
            oldReader = _reader;
            _reader = newReader;
        }

        oldReader?.Dispose();
    }

    private string GetDbPath()
    {
        var configRoot = _config.CurrentValue.ConfigRoot;
        return Path.Combine(configRoot, "geoip", "dbip-city-lite.mmdb");
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }
        return false;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _reader?.Dispose();
            _reader = null;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "GeoIP lookup failed for {Ip}")]
    private static partial void LogLookupFailed(ILogger logger, string ip, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading GeoIP database from {Url}")]
    private static partial void LogDownloadStarted(ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Information, Message = "GeoIP database downloaded ({Size} bytes)")]
    private static partial void LogDownloadCompleted(ILogger logger, long size);

    [LoggerMessage(Level = LogLevel.Error, Message = "GeoIP database download failed")]
    private static partial void LogDownloadFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "GeoIP database loaded from {Path}")]
    private static partial void LogDbLoaded(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load GeoIP database from {Path}")]
    private static partial void LogDbLoadFailed(ILogger logger, string path, Exception ex);
}
