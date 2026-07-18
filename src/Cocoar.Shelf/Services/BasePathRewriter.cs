using System.Text.RegularExpressions;

namespace Cocoar.Shelf.Services;

public static partial class BasePathRewriter
{
    /// <summary>
    /// Rewrites the base path in file content based on content type.
    /// For unique bases (e.g., "/__shelf__/", "/docs/"), a simple string replacement is used.
    /// For the root base "/", context-aware replacement is applied per content type.
    /// </summary>
    public static string Rewrite(string content, string originalBase, string targetBase, string contentType)
    {
        if (originalBase == targetBase)
            return content;

        // For unique bases (not "/"), simple string replacement is safe everywhere
        if (originalBase != "/")
            return content.Replace(originalBase, targetBase);

        // For base "/", we need context-aware replacement per content type
        if (contentType.Contains("text/html"))
            return RewriteHtml(content, targetBase);

        if (contentType.Contains("text/css"))
            return RewriteCss(content, targetBase);

        if (contentType.Contains("javascript"))
            return RewriteJs(content, targetBase);

        return content;
    }

    private static string RewriteHtml(string content, string targetBase)
    {
        // Replace href="/...", src="/..." attribute values
        // Negative lookahead (?!//) avoids matching protocol-relative URLs
        content = HtmlAttributeRegex().Replace(content, $"$1\"{targetBase}");

        // Replace base path in __VP_SITE_DATA__ JSON blob
        // VitePress inlines: window.__VP_SITE_DATA__=JSON.parse("{...\"base\":\"/\"...}")
        content = content.Replace(
            "\\\"base\\\":\\\"/\\\"",
            $"\\\"base\\\":\\\"{targetBase}\\\"");

        return content;
    }

    private static string RewriteCss(string content, string targetBase)
    {
        // Replace url(/...) font and asset references
        content = CssUrlRegex().Replace(content, $"url({targetBase}");
        return content;
    }

    private static string RewriteJs(string content, string targetBase)
    {
        // VitePress generates exactly 2 base-path references in framework JS.
        // We target those specifically to avoid false positives.

        // Pattern 1: Base path constant used by the VitePress router
        // Original: ){const n="/";t=pi(t.slice(n.length)
        // Target:   ){const n="/shelf/v1/";t=pi(t.slice(n.length)
        content = JsBaseConstRegex().Replace(content, $"){{const $1=\"{targetBase}\";$2");

        // Pattern 2: Modulepreload URL builder function
        // Original: =function(e){return"/"+e}
        // Target:   =function(e){return"/shelf/v1/"+e}
        content = JsUrlBuilderRegex().Replace(content, $"$1\"{targetBase}\"+$2");

        // Pattern 3: Search index document IDs contain full paths
        // These files have "documentIds" and all "/..." strings are document paths
        if (content.Contains("documentIds"))
        {
            content = JsStringLiteralRegex().Replace(content, $"\"{targetBase}");
        }

        return content;
    }

    // Matches: href="/..., src="/..., content="/... (not followed by /)
    [GeneratedRegex("""((?:href|src|content|action)=)"/(?!/)""")]
    private static partial Regex HtmlAttributeRegex();

    // Matches: url(/... (not followed by /)
    [GeneratedRegex("""url\(/(?!/)""")]
    private static partial Regex CssUrlRegex();

    // Matches: ){const n="/";t= (the VitePress router base constant)
    [GeneratedRegex(@"\)\{const (\w)=""/"";(\w=)")]
    private static partial Regex JsBaseConstRegex();

    // Matches: =function(e){return"/"+e} (the modulepreload URL builder)
    [GeneratedRegex(@"(=function\(\w\)\{return)""/""\+(\w\})")]
    private static partial Regex JsUrlBuilderRegex();

    // Matches: "/... where / is NOT followed by / (for search index paths)
    [GeneratedRegex(@"""\/(?!\/)")]
    private static partial Regex JsStringLiteralRegex();
}
