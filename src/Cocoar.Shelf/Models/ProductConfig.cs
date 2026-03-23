namespace Cocoar.Shelf.Models;

public class ProductConfig
{
    public required string Name { get; init; }

    public string? DisplayName { get; init; }

    public string? Description { get; init; }

    public string Source { get; init; } = "upload";

    public string Visibility { get; init; } = "public";

    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool ShowWhenEmpty { get; init; } = false;
}
