namespace Cocoar.Shelf.Models;

/// <summary>
/// Represents the available versions for a product, derived from the filesystem.
/// </summary>
public class ProductManifest
{
    public required string Latest { get; init; }

    public required IReadOnlyList<string> Versions { get; init; }
}
