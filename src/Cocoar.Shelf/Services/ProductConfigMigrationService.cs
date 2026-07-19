using System.Text.Json;
using Cocoar.Configuration.Reactive;
using Cocoar.Shelf.Models;
using Marten;

namespace Cocoar.Shelf.Services;

public sealed partial class ProductConfigMigrationService : IHostedService
{
    private readonly IDocumentStore _store;
    private readonly IReactiveConfig<ShelfOptions> _config;
    private readonly ILogger<ProductConfigMigrationService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public ProductConfigMigrationService(
        IDocumentStore store,
        IReactiveConfig<ShelfOptions> config,
        ILogger<ProductConfigMigrationService> logger)
    {
        _store = store;
        _config = config;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var session = _store.LightweightSession();

        // One-time seed: once seeded, the DB owns products — the JSON files are never read again, so a
        // product deleted via the UI/API is NOT resurrected from a lingering seed file on restart.
        if (await session.LoadAsync<SeedState>("products", cancellationToken) is not null)
            return;

        var productsDir = Path.Combine(_config.CurrentValue.ConfigRoot, "products");
        var jsonFiles = Directory.Exists(productsDir)
            ? Directory.GetFiles(productsDir, "*.json")
            : [];

        // Upgrade path: a DB seeded by the pre-marker version (or populated purely via the UI/API)
        // already has products — mark it seeded WITHOUT re-importing, so previously-deleted products
        // stay deleted.
        var alreadyPopulated = await session.Query<ProductConfig>().AnyAsync(cancellationToken);

        // Nothing to seed yet (empty DB, no seed files) — stay unseeded so a later boot with seed
        // files can still import them once.
        if (!alreadyPopulated && jsonFiles.Length == 0)
            return;

        var imported = 0;
        if (!alreadyPopulated)
        {
            foreach (var file in jsonFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, cancellationToken);
                    var config = JsonSerializer.Deserialize<ProductConfig>(json, JsonOptions);

                    if (config == null)
                    {
                        LogSkipped(_logger, file, "deserialized to null");
                        continue;
                    }

                    session.Store(config);
                    imported++;
                }
                catch (Exception ex)
                {
                    LogFailed(_logger, file, ex);
                }
            }
        }

        session.Store(new SeedState { Id = "products", SeededAt = DateTimeOffset.UtcNow });
        await session.SaveChangesAsync(cancellationToken);

        if (imported > 0)
            LogCompleted(_logger, imported, jsonFiles.Length);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped product config {File}: {Reason}")]
    private static partial void LogSkipped(ILogger logger, string file, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to import product config {File}")]
    private static partial void LogFailed(ILogger logger, string file, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Imported {Count}/{Total} product configs from JSON files into database")]
    private static partial void LogCompleted(ILogger logger, int count, int total);
}
