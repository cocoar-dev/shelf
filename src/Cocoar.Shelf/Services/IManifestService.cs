using Cocoar.Shelf.Models;

namespace Cocoar.Shelf.Services;

public interface IManifestService
{
    ProductManifest? GetManifest(string product);

    IReadOnlyList<string> GetProducts();
}
