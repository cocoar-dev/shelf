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

            var url = $"{pathBase}/{config.Name}/";

            cards.AppendLine(CultureInfo.InvariantCulture, $"""
                <a href="{Encode(url)}" target="_blank" class="card">
                    <div class="card-name">{Encode(config.DisplayName ?? config.Name)}</div>
                    <div class="card-desc">{Encode(config.Description ?? "")}</div>
                    <div class="card-meta">
                        <span class="card-latest">{Encode(manifest.Latest)}</span>
                        <span class="card-count">{manifest.Versions.Count} version{(manifest.Versions.Count != 1 ? "s" : "")}</span>
                    </div>
                </a>
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
                    text-decoration: none;
                    color: inherit;
                    transition: border-color 0.15s, box-shadow 0.15s;
                    display: flex;
                    flex-direction: column;
                }
                .card:hover {
                    border-color: #1183CD;
                    box-shadow: 0 2px 8px rgba(17, 131, 205, 0.12);
                }
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
                    flex: 1;
                }
                .card-meta {
                    margin-top: 16px;
                    display: flex;
                    gap: 12px;
                    font-size: 0.82em;
                    color: #94a3b8;
                }
                .card-latest {
                    background: #dbeafe;
                    color: #1183CD;
                    padding: 2px 8px;
                    border-radius: 4px;
                    font-weight: 500;
                }
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
