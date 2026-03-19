using Cocoar.Shelf.Models;

namespace Cocoar.Shelf.Services;

public interface IProductConfigService
{
    ProductConfig? GetConfig(string name);

    IReadOnlyList<ProductConfig> GetAll();
}
