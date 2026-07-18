namespace Cocoar.Shelf.Models;

public record CreateProductRequest(string Name, string? DisplayName, string? Description, string? Source, string? Visibility, IReadOnlyList<string>? Tags, bool? ShowWhenEmpty, string? ApiKey);

public record UpdateProductRequest(string? DisplayName, string? Description, string? Source, string? Visibility, IReadOnlyList<string>? Tags, bool? ShowWhenEmpty, string? ApiKey);
