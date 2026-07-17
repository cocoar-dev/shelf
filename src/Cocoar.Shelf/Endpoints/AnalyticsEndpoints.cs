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

        var totalVisits = entries.Count;
        var uniqueIps = entries.Select(x => x.Ip).Distinct().Count();

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
            .Take(20)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .ToList();

        var visitsByDay = entries
            .GroupBy(x => x.Timestamp.Date)
            .OrderBy(g => g.Key)
            .Select(g => new { Date = g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Count = g.Count() })
            .ToList();

        return Results.Ok(new
        {
            totalVisits,
            uniqueIps,
            topPages,
            topProducts,
            topCountries,
            visitsByDay
        });
    }
}
