using System.Globalization;
using Cocoar.Shelf.Models;
using Cocoar.Shelf.Services;
using Marten;

namespace Cocoar.Shelf.Endpoints;

public static class AnalyticsEndpoints
{
    public static RouteGroupBuilder MapAnalyticsEndpoints(this RouteGroupBuilder api)
    {
        // Access data carries visitor IPs — admin-only, not just any authenticated session.
        var analytics = api.MapGroup("/analytics").RequireAuthorization("Admin");

        analytics.MapGet("/visits", GetVisits);
        analytics.MapGet("/summary", GetSummary);
        analytics.MapGet("/geo/status", GetGeoStatus);
        analytics.MapPost("/geo/download", DownloadGeoDb);

        return api;
    }

    private static IResult GetGeoStatus(GeoIpService geoService)
    {
        return Results.Ok(new
        {
            loaded = geoService.IsLoaded,
            lastUpdated = geoService.LastUpdated
        });
    }

    private static async Task<IResult> DownloadGeoDb(
        GeoIpService geoService,
        CancellationToken ct)
    {
        var (success, error) = await geoService.DownloadAsync(ct);

        if (!success)
            return Results.Json(new { error }, statusCode: 500);

        return Results.Ok(new
        {
            ok = true,
            loaded = geoService.IsLoaded,
            lastUpdated = geoService.LastUpdated
        });
    }

    private static async Task<IResult> GetVisits(
        IQuerySession session,
        string? product = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? excludeIps = null,
        int limit = 100,
        int offset = 0,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 1000);
        offset = Math.Max(offset, 0);

        IQueryable<AccessLogEntry> query = session.Query<AccessLogEntry>();

        if (!string.IsNullOrEmpty(product))
            query = query.Where(x => x.Product == product);

        if (from.HasValue)
            query = query.Where(x => x.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.Timestamp <= to.Value);

        var excluded = ParseCsvList(excludeIps);
        if (excluded.Count > 0)
            query = query.Where(x => !excluded.Contains(x.Ip));

        var total = await query.CountAsync(ct);

        var entries = await query
            .OrderByDescending(x => x.Timestamp)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

        return Results.Ok(new { total, entries });
    }

    private static async Task<IResult> GetSummary(
        IQuerySession session,
        string? product = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? excludeIps = null,
        CancellationToken ct = default)
    {
        IQueryable<AccessLogEntry> query = session.Query<AccessLogEntry>();

        if (!string.IsNullOrEmpty(product))
            query = query.Where(x => x.Product == product);

        if (from.HasValue)
            query = query.Where(x => x.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.Timestamp <= to.Value);

        var entries = await query.ToListAsync(ct);

        // IP exclusion (e.g. hide the operator's own IP) — applied to every aggregation below.
        var excluded = ParseCsvSet(excludeIps);
        if (excluded.Count > 0)
            entries = entries.Where(x => !excluded.Contains(x.Ip)).ToList();

        var totalVisits = entries.Count;
        var uniqueIps = entries.Select(x => x.Ip).Distinct().Count();
        var countriesCount = entries.Where(x => x.Country != null).Select(x => x.Country).Distinct().Count();
        var citiesCount = entries.Where(x => x.City != null).Select(x => x.City).Distinct().Count();

        var visitsByDay = entries
            .GroupBy(x => x.Timestamp.Date)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Date = g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Count = g.Count(),
                UniqueIps = g.Select(e => e.Ip).Distinct().Count(),
            })
            .ToList();

        var topPages = entries
            .GroupBy(x => $"{x.Product}/{x.Version}/{x.Path}")
            .OrderByDescending(g => g.Count())
            .Take(20)
            .Select(g => new { Page = g.Key, Count = g.Count() })
            .ToList();

        var topProducts = entries
            .GroupBy(x => x.Product)
            .OrderByDescending(g => g.Count())
            .Take(20)
            .Select(g => new { Product = g.Key, Count = g.Count() })
            .ToList();

        var topCountries = entries
            .Where(x => x.Country != null)
            .GroupBy(x => x.Country)
            .OrderByDescending(g => g.Count())
            .Take(30)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .ToList();

        var topCities = entries
            .Where(x => x.City != null)
            .GroupBy(x => new { x.City, x.Country })
            .OrderByDescending(g => g.Count())
            .Take(30)
            .Select(g => new { g.Key.City, g.Key.Country, Count = g.Count() })
            .ToList();

        // Map points: aggregate geolocated entries by city, with a representative lat/lng.
        var locations = entries
            .Where(x => x is { Latitude: not null, Longitude: not null })
            .GroupBy(x => new { x.City, x.Country })
            .OrderByDescending(g => g.Count())
            .Select(g => new
            {
                g.Key.City,
                g.Key.Country,
                Lat = g.First().Latitude,
                Lng = g.First().Longitude,
                Count = g.Count(),
            })
            .ToList();

        var byBrowser = entries
            .GroupBy(x => ClassifyBrowser(x.UserAgent))
            .OrderByDescending(g => g.Count())
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToList();

        var byOs = entries
            .GroupBy(x => ClassifyOs(x.UserAgent))
            .OrderByDescending(g => g.Count())
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToList();

        var byLanguage = entries
            .Select(x => PrimaryLanguage(x.AcceptLanguage))
            .Where(l => l != null)
            .GroupBy(l => l)
            .OrderByDescending(g => g.Count())
            .Take(20)
            .Select(g => new { Language = g.Key, Count = g.Count() })
            .ToList();

        var topReferrers = entries
            .Select(x => ReferrerHost(x.Referer))
            .Where(r => r != null)
            .GroupBy(r => r)
            .OrderByDescending(g => g.Count())
            .Take(20)
            .Select(g => new { Referrer = g.Key, Count = g.Count() })
            .ToList();

        return Results.Ok(new
        {
            totalVisits,
            uniqueIps,
            countriesCount,
            citiesCount,
            visitsByDay,
            topPages,
            topProducts,
            topCountries,
            topCities,
            locations,
            byBrowser,
            byOs,
            byLanguage,
            topReferrers,
        });
    }

    private static List<string> ParseCsvList(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static HashSet<string> ParseCsvSet(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? new HashSet<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Lightweight server-side User-Agent classification — no browser-side parsing.
    private static string ClassifyBrowser(string? ua)
    {
        if (string.IsNullOrEmpty(ua)) return "Unknown";
        if (ua.Contains("bot", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("spider", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("crawl", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("curl", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("wget", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("python", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("Go-http", StringComparison.Ordinal)) return "Bot / Script";
        if (ua.Contains("Edg/", StringComparison.Ordinal) || ua.Contains("Edge/", StringComparison.Ordinal)) return "Edge";
        if (ua.Contains("OPR/", StringComparison.Ordinal) || ua.Contains("Opera", StringComparison.Ordinal)) return "Opera";
        if (ua.Contains("Firefox/", StringComparison.Ordinal)) return "Firefox";
        if (ua.Contains("Chrome/", StringComparison.Ordinal)) return "Chrome";
        if (ua.Contains("Safari/", StringComparison.Ordinal)) return "Safari";
        return "Other";
    }

    private static string ClassifyOs(string? ua)
    {
        if (string.IsNullOrEmpty(ua)) return "Unknown";
        if (ua.Contains("Windows", StringComparison.Ordinal)) return "Windows";
        if (ua.Contains("Android", StringComparison.Ordinal)) return "Android";
        if (ua.Contains("iPhone", StringComparison.Ordinal) || ua.Contains("iPad", StringComparison.Ordinal)) return "iOS";
        if (ua.Contains("Mac OS X", StringComparison.Ordinal) || ua.Contains("Macintosh", StringComparison.Ordinal)) return "macOS";
        if (ua.Contains("Linux", StringComparison.Ordinal)) return "Linux";
        return "Other";
    }

    private static string? PrimaryLanguage(string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage)) return null;
        // "de-AT,de;q=0.9,en;q=0.8" → "de-AT"
        var first = acceptLanguage.Split(',', 2)[0].Split(';', 2)[0].Trim();
        return string.IsNullOrEmpty(first) ? null : first;
    }

    private static string? ReferrerHost(string? referer)
    {
        if (string.IsNullOrWhiteSpace(referer)) return null;
        return Uri.TryCreate(referer, UriKind.Absolute, out var uri) ? uri.Host : null;
    }
}
