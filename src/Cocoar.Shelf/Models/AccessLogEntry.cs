namespace Cocoar.Shelf.Models;

public class AccessLogEntry
{
    public Guid Id { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string Ip { get; set; } = "";

    public string? Product { get; set; }

    public string? Version { get; set; }

    public string? Path { get; set; }

    public string? UserAgent { get; set; }

    public string? Referer { get; set; }

    public string? AcceptLanguage { get; set; }

    /// <summary>Two-letter ISO country code (e.g. "AT"). The full name + flag are derived client-side.</summary>
    public string? Country { get; set; }

    public string? City { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }
}
