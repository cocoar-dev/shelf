namespace Cocoar.Shelf.Models;

/// <summary>
/// Marker that the one-time product JSON seed has run. Once present, the migration never re-reads the
/// JSON files again — the database is the source of truth, so a deleted product stays deleted across
/// restarts (it is not resurrected from a lingering seed file).
/// </summary>
public class SeedState
{
    /// <summary>Well-known single id.</summary>
    public string Id { get; set; } = "products";

    public DateTimeOffset SeededAt { get; set; }
}
