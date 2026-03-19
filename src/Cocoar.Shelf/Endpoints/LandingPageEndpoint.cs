using System.Globalization;
using System.Net;
using System.Text;
using Cocoar.Shelf.Services;

namespace Cocoar.Shelf.Endpoints;

public static class LandingPageEndpoint
{
    public static IResult Render(
        HttpContext httpContext,
        IProductConfigService configService,
        IManifestService manifestService)
    {
        var pathBase = httpContext.Request.PathBase.Value ?? "";
        var products = configService.GetAll(); // sorted by name

        var cards = new StringBuilder();

        foreach (var config in products)
        {
            var manifest = manifestService.GetManifest(config.Name);
            if (manifest == null) continue;

            var latestUrl = $"{pathBase}/{config.Name}/";

            var badges = new StringBuilder();
            foreach (var version in manifest.Versions)
            {
                var versionUrl = $"{pathBase}/{config.Name}/{version}/";
                var isLatest = version == manifest.Latest;
                var cssClass = isLatest ? "version-badge latest" : "version-badge";
                badges.Append(CultureInfo.InvariantCulture,
                    $"""<a href="{Encode(versionUrl)}" target="_blank" class="{cssClass}">{Encode(version)}</a>""");
            }

            cards.AppendLine(CultureInfo.InvariantCulture, $"""
                <div class="card">
                    <a href="{Encode(latestUrl)}" target="_blank" class="card-link">
                        <div class="card-name">{Encode(config.DisplayName ?? config.Name)}</div>
                        <div class="card-desc">{Encode(config.Description ?? "")}</div>
                    </a>
                    <div class="card-versions">{badges}</div>
                </div>
                """);
        }

        var html = Template.Replace("{cards}", cards.ToString());

        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private const string Template = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Documentation</title>
            <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
                    color: #1a1a1a;
                    background: #f8fafc;
                    min-height: 100vh;
                }
                header {
                    background: #1183CD;
                    color: white;
                    padding: 20px 24px;
                    text-align: center;
                }
                header h1 {
                    font-size: 1.5em;
                    font-weight: 600;
                }
                header p {
                    margin-top: 4px;
                    font-size: 0.95em;
                    opacity: 0.85;
                }
                .grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
                    gap: 20px;
                    max-width: 1200px;
                    margin: 40px auto;
                    padding: 0 24px;
                }
                .card {
                    background: white;
                    border: 1px solid #e2e8f0;
                    border-radius: 8px;
                    padding: 24px;
                    display: flex;
                    flex-direction: column;
                    transition: border-color 0.15s, box-shadow 0.15s;
                }
                .card:hover {
                    border-color: #1183CD;
                    box-shadow: 0 2px 8px rgba(17, 131, 205, 0.12);
                }
                .card-link {
                    text-decoration: none;
                    color: inherit;
                    flex: 1;
                }
                .card-link:hover .card-name { color: #0E6DB0; }
                .card-name {
                    font-size: 1.15em;
                    font-weight: 600;
                    color: #1183CD;
                    margin-bottom: 6px;
                }
                .card-desc {
                    font-size: 0.9em;
                    color: #64748b;
                    line-height: 1.5;
                }
                .card-versions {
                    margin-top: 16px;
                    padding-top: 14px;
                    border-top: 1px solid #f1f1f2;
                    display: flex;
                    flex-wrap: wrap;
                    gap: 6px;
                }
                .version-badge {
                    display: inline-block;
                    padding: 2px 10px;
                    border-radius: 10px;
                    font-size: 0.78em;
                    font-weight: 500;
                    text-decoration: none;
                    background: #f6f6f7;
                    color: #64748b;
                    border: 1px solid #e2e8f0;
                    transition: border-color 0.15s, color 0.15s;
                }
                .version-badge:hover {
                    border-color: #1183CD;
                    color: #1183CD;
                }
                .version-badge.latest {
                    background: #dbeafe;
                    color: #1183CD;
                    border-color: #bfdbfe;
                    font-weight: 600;
                }
                .version-badge.latest:hover { background: #bfdbfe; }
                .empty {
                    text-align: center;
                    color: #94a3b8;
                    padding: 80px 24px;
                    font-size: 1.1em;
                }
            </style>
        </head>
        <body>
            <header>
                <h1>Documentation</h1>
            </header>
            <div class="grid">{cards}</div>
        </body>
        </html>
        """;
}
