using Cocoar.Shelf.Models;

namespace Cocoar.Shelf.Services;

/// <summary>
/// CRUD over <see cref="Group"/> permission carriers (Access Control v2). Soft-delete: <see
/// cref="DeleteAsync"/> flips <see cref="Group.IsDeleted"/> and the read methods exclude deleted
/// groups by default, so a deleted group grants nothing.
/// </summary>
public interface IGroupService
{
    Task<Group?> GetAsync(Guid id);

    Task<IReadOnlyList<Group>> GetAllAsync();

    Task CreateAsync(Group group);

    Task UpdateAsync(Group group);

    Task<bool> DeleteAsync(Guid id);
}
