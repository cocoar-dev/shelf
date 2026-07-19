namespace Cocoar.Shelf.Models;

/// <summary>
/// Whether a product's source code is publicly available. Purely a display/labeling dimension
/// (orthogonal to <see cref="ProductConfig.Visibility"/> and <see cref="ProductConfig.Restricted"/>):
/// it drives a badge on the landing page so proprietary docs can be hosted alongside open-source ones
/// and be clearly distinguished. <see cref="Unspecified"/> ⇒ no badge (unchanged behavior).
/// </summary>
public enum ProductOpenness
{
    Unspecified = 0,
    OpenSource = 1,
    Proprietary = 2,
}
