using System.Globalization;
using System.Text;
using Cocoar.Shelf.Services;

namespace Cocoar.Shelf.Endpoints;

public static class LlmsTxtEndpoint
{
    public static WebApplication MapLlmsTxt(this WebApplication app)
    {
        app.MapGet("/llms.txt", Generate);
        return app;
    }

    private static async Task<IResult> Generate(
        HttpContext httpContext,
        IProductConfigService configService,
        IManifestService manifestService)
    {
        var pathBase = httpContext.Request.PathBase.Value ?? "";
        var ci = CultureInfo.InvariantCulture;

        var sb = new StringBuilder();
        sb.AppendLine("# Documentation Index");
        sb.AppendLine();
        sb.AppendLine(ci, $"> Documentation hosting for Cocoar products");
        sb.AppendLine();

        var products = await configService.GetAllAsync();
        var hasProducts = false;

        foreach (var config in products)
        {
            if (!string.Equals(config.Visibility, "public", StringComparison.OrdinalIgnoreCase))
                continue;

            // The public LLM index never lists restricted products.
            if (config.Restricted)
                continue;

            var manifest = manifestService.GetManifest(config.Name);
            if (manifest == null)
                continue;

            var stableVersions = manifest.Versions
                .Where(v => !v.Contains('-'))
                .ToList();

            if (stableVersions.Count == 0)
                continue;

            if (!hasProducts)
            {
                sb.AppendLine("## Products");
                sb.AppendLine();
                hasProducts = true;
            }

            var latest = stableVersions[0];
            var docsUrl = $"{pathBase}/{config.Name}/{latest}/";

            sb.AppendLine(ci, $"### {config.DisplayName ?? config.Name}");
            if (!string.IsNullOrEmpty(config.Description))
                sb.AppendLine(ci, $"- Description: {config.Description}");
            sb.AppendLine(ci, $"- Latest: {latest}");
            sb.AppendLine(ci, $"- Versions: {string.Join(", ", stableVersions)}");
            sb.AppendLine(ci, $"- Docs: {docsUrl}");
            sb.AppendLine(ci, $"- LLM Docs: {docsUrl}llms-full.txt");
            sb.AppendLine();
        }

        if (!hasProducts)
        {
            sb.AppendLine("No documentation available yet.");
            sb.AppendLine();
        }

        return Results.Text(sb.ToString(), "text/plain; charset=utf-8");
    }
}
