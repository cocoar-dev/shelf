using Cocoar.Shelf.Models;
using Marten;

namespace Cocoar.Shelf.Services;

public sealed class MartenGroupService(IDocumentStore store) : IGroupService
{
    public async Task<Group?> GetAsync(Guid id)
    {
        await using var session = store.QuerySession();
        var group = await session.LoadAsync<Group>(id);
        return group is { IsDeleted: false } ? group : null;
    }

    public async Task<IReadOnlyList<Group>> GetAllAsync()
    {
        await using var session = store.QuerySession();
        return await session.Query<Group>()
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task CreateAsync(Group group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group.Name);
        if (group.Id == Guid.Empty)
            throw new ArgumentException("Group Id must be set before create.", nameof(group));

        await using var session = store.LightweightSession();
        session.Store(group);
        await session.SaveChangesAsync();
    }

    public async Task UpdateAsync(Group group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group.Name);

        await using var session = store.LightweightSession();
        var existing = await session.LoadAsync<Group>(group.Id);
        if (existing is null || existing.IsDeleted)
            throw new KeyNotFoundException($"Group '{group.Id}' not found");

        session.Store(group);
        await session.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await using var session = store.LightweightSession();
        var existing = await session.LoadAsync<Group>(id);
        if (existing is null || existing.IsDeleted)
            return false;

        existing.IsDeleted = true;
        session.Store(existing);
        await session.SaveChangesAsync();
        return true;
    }
}
