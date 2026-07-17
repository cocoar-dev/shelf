namespace Cocoar.Shelf.Models;

public class ProductConfig
{
    public required string Name { get; set; }

    public string? DisplayName { get; set; }

    public string? Description { get; set; }

    public string Source { get; set; } = "upload";

    public string Visibility { get; set; } = "public";

    public IReadOnlyList<string> Tags { get; set; } = [];

    public bool ShowWhenEmpty { get; set; }

    public string? ApiKey { get; set; }
}
