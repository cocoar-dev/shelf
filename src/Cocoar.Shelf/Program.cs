using Cocoar.Configuration.AspNetCore;
using Cocoar.Configuration.DI.Extensions;
using Cocoar.Configuration.Providers;
using Cocoar.Configuration.Reactive;
using Cocoar.Shelf;
using Cocoar.Shelf.Endpoints;
using Cocoar.Shelf.Middleware;
using Cocoar.Shelf.Services;
using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.AddCocoarConfiguration(c => c
    .UseConfiguration(rules => [
        rules.For<ShelfOptions>().FromFile("data/configuration.json"),
        rules.For<ShelfOptions>().FromEnvironment("Shelf__"),
    ]));

var configManager = builder.GetCocoarConfigManager();
var config = configManager.GetConfig<ShelfOptions>()!;

builder.Services.AddSerilog(logConfig =>
{
    foreach (var (key, level) in config.Logging.LogLevels)
    {
        if (key.Equals("default", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("*", StringComparison.OrdinalIgnoreCase))
        {
            logConfig.MinimumLevel.Is(level);
        }
        else
        {
            logConfig.MinimumLevel.Override(key, level);
        }
    }

    logConfig.WriteTo.Console(theme: AnsiConsoleTheme.Code, formatProvider: CultureInfo.InvariantCulture);
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "shelf.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<IManifestService, ManifestService>();
builder.Services.AddSingleton<IProductConfigService, ProductConfigService>();
builder.Services.AddSingleton<IUploadService, UploadService>();
builder.Services.AddSingleton<BasePathDetector>();

var app = builder.Build();

if (!string.IsNullOrEmpty(config.PathBase))
    app.UsePathBase(config.PathBase);

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-XSS-Protection"] = "0";
    await next();
});

app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapApiEndpoints();
app.MapLlmsTxt();
app.UseMiddleware<DocsRoutingMiddleware>();
app.MapFallback(async (HttpContext ctx, IReactiveConfig<ShelfOptions> shelfConfig) =>
{
    var env = ctx.RequestServices.GetRequiredService<IWebHostEnvironment>();
    var indexPath = Path.Combine(env.WebRootPath, "index.html");
    if (!File.Exists(indexPath))
    {
        ctx.Response.StatusCode = 404;
        return;
    }
    var html = await File.ReadAllTextAsync(indexPath);
    var pathBase = shelfConfig.CurrentValue.PathBase.TrimEnd('/');
    html = html.Replace(
        "window.__SHELF_OPTIONS__ = {\"pathBase\":\"\"};",
        $"window.__SHELF_OPTIONS__ = {{\"pathBase\":\"{pathBase}\"}};");
    ctx.Response.ContentType = "text/html; charset=utf-8";
    await ctx.Response.WriteAsync(html);
});

app.Run(config.AppUrl);

public partial class Program;
