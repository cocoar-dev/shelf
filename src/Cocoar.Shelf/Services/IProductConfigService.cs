using Cocoar.Shelf.Models;

namespace Cocoar.Shelf.Services;

public interface IProductConfigService
{
    ProductConfig? GetConfig(string name);

    IReadOnlyList<ProductConfig> GetAll();

    Task CreateAsync(ProductConfig config);

    Task UpdateAsync(ProductConfig config);

    Task<bool> DeleteAsync(string name);
}
