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
        var productsDir = Path.Combine(_config.CurrentValue.ConfigRoot, "products");

        if (!Directory.Exists(productsDir))
            return;

        var jsonFiles = Directory.GetFiles(productsDir, "*.json");
        if (jsonFiles.Length == 0)
            return;

        await using var session = _store.LightweightSession();
        var imported = 0;

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

                var existing = await session.LoadAsync<ProductConfig>(config.Name, cancellationToken);
                if (existing != null)
                    continue; // Already in DB

                session.Store(config);
                imported++;
            }
            catch (Exception ex)
            {
                LogFailed(_logger, file, ex);
            }
        }

        if (imported > 0)
        {
            await session.SaveChangesAsync(cancellationToken);
            LogCompleted(_logger, imported, jsonFiles.Length);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped product config {File}: {Reason}")]
    private static partial void LogSkipped(ILogger logger, string file, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to import product config {File}")]
    private static partial void LogFailed(ILogger logger, string file, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Imported {Count}/{Total} product configs from JSON files into database")]
    private static partial void LogCompleted(ILogger logger, int count, int total);
}
