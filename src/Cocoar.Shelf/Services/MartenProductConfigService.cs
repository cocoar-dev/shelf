using Cocoar.Shelf.Models;
using Marten;

namespace Cocoar.Shelf.Services;

public sealed class MartenProductConfigService : IProductConfigService
{
    private readonly IDocumentStore _store;

    public MartenProductConfigService(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<ProductConfig?> GetConfigAsync(string name)
    {
        await using var session = _store.QuerySession();
        return await session.LoadAsync<ProductConfig>(name);
    }

    public async Task<IReadOnlyList<ProductConfig>> GetAllAsync()
    {
        await using var session = _store.QuerySession();
        return await session.Query<ProductConfig>()
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task CreateAsync(ProductConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Name);

        await using var session = _store.LightweightSession();

        var existing = await session.LoadAsync<ProductConfig>(config.Name);
        if (existing != null)
            throw new InvalidOperationException($"Product '{config.Name}' already exists");

        session.Store(config);
        await session.SaveChangesAsync();
    }

    public async Task UpdateAsync(ProductConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Name);

        await using var session = _store.LightweightSession();

        var existing = await session.LoadAsync<ProductConfig>(config.Name);
        if (existing == null)
            throw new KeyNotFoundException($"Product '{config.Name}' not found");

        session.Store(config);
        await session.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using var session = _store.LightweightSession();

        var existing = await session.LoadAsync<ProductConfig>(name);
        if (existing == null)
            return false;

        session.Delete(existing);
        await session.SaveChangesAsync();
        return true;
    }
}
