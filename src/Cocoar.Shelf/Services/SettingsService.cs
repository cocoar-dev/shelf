using Cocoar.Shelf.Models;
using Marten;

namespace Cocoar.Shelf.Services;

public interface ISettingsService
{
    /// <summary>The current global settings (cached; never null).</summary>
    ShelfSettings Current { get; }

    Task<ShelfSettings> UpdateAsync(Action<ShelfSettings> mutate, CancellationToken ct = default);
}

/// <summary>
/// Cached accessor for the single <see cref="ShelfSettings"/> document. The cache makes the
/// per-request read in <see cref="Endpoints.ApiKeyFilter"/> free; updates write through.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IDocumentStore _store;
    private readonly Lock _lock = new();
    private ShelfSettings? _cached;

    public SettingsService(IDocumentStore store)
    {
        _store = store;
    }

    public ShelfSettings Current
    {
        get
        {
            if (_cached is { } cached) return cached;
            lock (_lock)
            {
                if (_cached is null)
                {
                    using var session = _store.QuerySession();
                    _cached = session.LoadAsync<ShelfSettings>(ShelfSettings.GlobalId)
                        .GetAwaiter().GetResult() ?? new ShelfSettings();
                }
                return _cached;
            }
        }
    }

    public async Task<ShelfSettings> UpdateAsync(Action<ShelfSettings> mutate, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var settings = await session.LoadAsync<ShelfSettings>(ShelfSettings.GlobalId, ct) ?? new ShelfSettings();
        mutate(settings);
        session.Store(settings);
        await session.SaveChangesAsync(ct);
        lock (_lock) { _cached = settings; }
        return settings;
    }
}
