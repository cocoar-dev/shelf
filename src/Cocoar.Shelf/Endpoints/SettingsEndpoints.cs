using Cocoar.Shelf.Services;

namespace Cocoar.Shelf.Endpoints;

public record SetMasterApiKeyRequest(string? ApiKey);

/// <summary>
/// Admin-managed global settings. Shelf hosts documentation, not secrets — API keys are
/// deliberately readable for admins (unlike an IdP, losing one is a nuisance, not a breach).
/// </summary>
public static partial class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder api)
    {
        var settings = api.MapGroup("/settings").RequireAuthorization("Admin");

        settings.MapGet("/", GetSettings);
        settings.MapPut("/api-key", SetMasterApiKey);

        return api;
    }

    private static IResult GetSettings(ISettingsService settingsService, ShelfOptions options) =>
        Results.Ok(new
        {
            hasMasterApiKey = !string.IsNullOrEmpty(settingsService.Current.MasterApiKey),
            masterApiKey = settingsService.Current.MasterApiKey,
            // The env/config bootstrap key stays valid alongside the UI-managed one.
            hasConfigApiKey = !string.IsNullOrEmpty(options.ApiKey),
        });

    private static async Task<IResult> SetMasterApiKey(
        SetMasterApiKeyRequest request,
        ISettingsService settingsService,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Settings");
        var key = string.IsNullOrWhiteSpace(request.ApiKey) ? null : request.ApiKey.Trim();

        if (key is { Length: < 16 })
            return Results.Json(new { error = "API key must be at least 16 characters" }, statusCode: 400);

        await settingsService.UpdateAsync(s => s.MasterApiKey = key, ct);

        if (key is null) LogMasterKeyCleared(logger);
        else LogMasterKeyUpdated(logger);

        return Results.Ok(new { hasMasterApiKey = key is not null });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Master API key cleared")]
    private static partial void LogMasterKeyCleared(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Master API key updated")]
    private static partial void LogMasterKeyUpdated(ILogger logger);
}
