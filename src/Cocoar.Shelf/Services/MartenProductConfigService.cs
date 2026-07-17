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

    public ProductConfig? GetConfig(string name)
    {
        using var session = _store.QuerySession();
        return session.LoadAsync<ProductConfig>(name).GetAwaiter().GetResult();
    }

    public IReadOnlyList<ProductConfig> GetAll()
    {
        using var session = _store.QuerySession();
        return session.Query<ProductConfig>()
            .OrderBy(x => x.Name)
            .ToList();
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
