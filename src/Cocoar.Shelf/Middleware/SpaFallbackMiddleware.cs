namespace Cocoar.Shelf.Middleware;

public class SpaFallbackMiddleware(RequestDelegate next, IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        // Serve the SPA index.html for unhandled GET/HEAD requests that returned 404.
        // Skip if the request was already handled by docs routing (real 404 for missing docs files).
        if (!context.Response.HasStarted &&
            context.Response.StatusCode == 404 &&
            !context.Items.ContainsKey("DocsRouted") &&
            (context.Request.Method == HttpMethods.Get || context.Request.Method == HttpMethods.Head))
        {
            var indexPath = Path.Combine(env.WebRootPath ?? "", "index.html");
            if (File.Exists(indexPath))
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html";
                context.Response.Headers.CacheControl = "no-cache";
                await context.Response.SendFileAsync(indexPath);
            }
        }
    }
}
