using Cocoar.Configuration.Reactive;
using Cocoar.Shelf.Models;
using Marten;

namespace Cocoar.Shelf.Services;

public sealed partial class AccessLogPersistenceService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly AccessLogChannel _channel;
    private readonly GeoIpService _geoIpService;
    private readonly IReactiveConfig<ShelfOptions> _config;
    private readonly ILogger<AccessLogPersistenceService> _logger;

    public AccessLogPersistenceService(
        IServiceProvider services,
        AccessLogChannel channel,
        GeoIpService geoIpService,
        IReactiveConfig<ShelfOptions> config,
        ILogger<AccessLogPersistenceService> logger)
    {
        _services = services;
        _channel = channel;
        _geoIpService = geoIpService;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = CleanupLoop(stoppingToken);

        await foreach (var entry in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                EnrichWithGeoData(entry);

                using var scope = _services.CreateScope();
                await using var session = scope.ServiceProvider
                    .GetRequiredService<IDocumentStore>()
                    .LightweightSession();

                session.Store(entry);
                await session.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogPersistFailed(_logger, ex);
            }
        }
    }

    private void EnrichWithGeoData(AccessLogEntry entry)
    {
        if (string.IsNullOrEmpty(entry.Ip))
            return;

        var geo = _geoIpService.Lookup(entry.Ip);
        if (geo != null)
        {
            entry.Country = geo.Value.CountryCode;
            entry.City = geo.Value.City;
            entry.Latitude = geo.Value.Latitude;
            entry.Longitude = geo.Value.Longitude;
        }
    }

    private async Task CleanupLoop(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var retentionDays = _config.CurrentValue.AccessLog.RetentionDays;
                var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);

                using var scope = _services.CreateScope();
                await using var session = scope.ServiceProvider
                    .GetRequiredService<IDocumentStore>()
                    .LightweightSession();

                session.DeleteWhere<AccessLogEntry>(x => x.Timestamp < cutoff);
                await session.SaveChangesAsync(stoppingToken);

                LogCleanupCompleted(_logger, retentionDays);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCleanupFailed(_logger, ex);
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to persist access log entry")]
    private static partial void LogPersistFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "GeoIP lookup failed for {Ip}")]
    private static partial void LogGeoLookupFailed(ILogger logger, string ip, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Access log cleanup completed (retention: {Days} days)")]
    private static partial void LogCleanupCompleted(ILogger logger, int days);

    [LoggerMessage(Level = LogLevel.Error, Message = "Access log cleanup failed")]
    private static partial void LogCleanupFailed(ILogger logger, Exception ex);
}
