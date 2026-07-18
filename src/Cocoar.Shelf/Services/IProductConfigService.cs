using Cocoar.Shelf.Models;

namespace Cocoar.Shelf.Services;

public interface IProductConfigService
{
    Task<ProductConfig?> GetConfigAsync(string name);

    Task<IReadOnlyList<ProductConfig>> GetAllAsync();

    Task CreateAsync(ProductConfig config);

    Task UpdateAsync(ProductConfig config);

    Task<bool> DeleteAsync(string name);
}
