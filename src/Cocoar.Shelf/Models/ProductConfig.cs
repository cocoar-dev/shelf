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

    /// <summary>
    /// Principals (groups and/or individual users) granted read access when <see cref="Restricted"/>.
    /// Product-side, principal-oriented grant — you pick who may read here, next to the toggle.
    /// </summary>
    public IReadOnlyList<PrincipalRef> ReadPrincipals { get; set; } = [];

    public IReadOnlyList<string> Tags { get; set; } = [];

    public bool ShowWhenEmpty { get; set; }

    /// <summary>
    /// Whether this product's source is public. Display-only: drives a landing-page badge so
    /// open-source and proprietary docs can be hosted side by side and clearly told apart.
    /// Default <see cref="ProductOpenness.Unspecified"/> ⇒ no badge.
    /// </summary>
    public ProductOpenness Openness { get; set; } = ProductOpenness.Unspecified;

    /// <summary>
    /// Optional source-repository URL. When set, the landing card shows a "Source ↗" link — typically
    /// only for open-source products; leave empty so proprietary docs carry no repo link.
    /// </summary>
    public string? RepositoryUrl { get; set; }

    public string? ApiKey { get; set; }
}
