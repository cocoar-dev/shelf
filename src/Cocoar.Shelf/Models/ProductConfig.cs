namespace Cocoar.Shelf.Models;

public class ProductConfig
{
    public required string Name { get; set; }

    public string? DisplayName { get; set; }

    public string? Description { get; set; }

    public string Source { get; set; } = "upload";

    public string Visibility { get; set; } = "public";

    /// <summary>
    /// Access control (Access Control v2), orthogonal to <see cref="Visibility"/>: when true the
    /// product and all its docs are hidden from users without a read grant and return 404 on
    /// unauthorized access. <see cref="Visibility"/> stays a display/maturity dimension, so a product
    /// can be preview+restricted or stable+restricted. Default false ⇒ public, unchanged behavior.
    /// </summary>
    public bool Restricted { get; set; }

    public IReadOnlyList<string> Tags { get; set; } = [];

    public bool ShowWhenEmpty { get; set; }

    public string? ApiKey { get; set; }
}
