namespace Cocoar.Shelf.Models;

public record CreateProductRequest(string Name, string? DisplayName, string? Description, string? Source, string? Visibility, IReadOnlyList<string>? Tags, bool? ShowWhenEmpty);

public record UpdateProductRequest(string? DisplayName, string? Description, string? Source, string? Visibility, IReadOnlyList<string>? Tags, bool? ShowWhenEmpty);
