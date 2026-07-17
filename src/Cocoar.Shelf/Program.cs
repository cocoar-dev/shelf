using Cocoar.Configuration.AspNetCore;
using Cocoar.Configuration.DI.Extensions;
using Cocoar.Configuration.Providers;
using Cocoar.Configuration.Reactive;
using Cocoar.Shelf;
using Cocoar.Shelf.Endpoints;
using Cocoar.Shelf.Identity;
using Cocoar.Shelf.Middleware;
using Cocoar.Shelf.Models;
using Cocoar.Shelf.Services;
using System.Globalization;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
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

// --- Database (Marten/PostgreSQL) — required ---
if (string.IsNullOrEmpty(config.Database.ConnectionString))
    throw new InvalidOperationException(
        "Shelf requires PostgreSQL: set Database.ConnectionString (env Shelf__Database__ConnectionString).");

builder.Services.AddMarten(opts =>
{
    opts.Connection(config.Database.ConnectionString);

    // Users (thin local mirror of modgud identities, Id == modgud sub)
    opts.Schema.For<UserDocument>()
        .DatabaseSchemaName("shelf")
        .UniqueIndex(x => x.NormalizedUserName)
        .Index(x => x.NormalizedEmail)
        .Index(x => x.IsActive);

    // Access log
    opts.Schema.For<AccessLogEntry>()
        .DatabaseSchemaName("shelf")
        .Index(x => x.Timestamp)
        .Index(x => x.Product)
        .Index(x => x.Ip);

    // Product config
    opts.Schema.For<ProductConfig>()
        .DatabaseSchemaName("shelf")
        .Identity(x => x.Name);
})
.UseLightweightSessions()
.ApplyAllDatabaseChangesOnStartup();

builder.Services.AddHttpClient("geoip");
builder.Services.AddSingleton<GeoIpService>();

// --- Identity + modgud federation ---
// modgud (external IdP) owns all credentials; login is brokered server-to-server (ModgudLoginBroker)
// and mints the cookie session. ASP.NET Identity plumbing stays ONLY to carry the cookie
// (SecurityStamp pipeline) and as the integration tests' sign-in seam — it is not a product login.
builder.Services.AddIdentityCore<UserDocument>()
    .AddSignInManager()
    .AddUserStore<MartenUserStore>();

// AddIdentityCore doesn't register the stamp validator the cookie's ValidatePrincipal hook uses.
builder.Services.AddScoped<ISecurityStampValidator, SecurityStampValidator<UserDocument>>();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "shelf.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        // Keep the modgud RBAC snapshot (resource_access claim) alive across the security-stamp
        // validator's periodic principal regeneration.
        options.Events.OnValidatePrincipal = RbacCookiePreservation.ValidatePreservingRbacAsync;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
        };
    });

builder.Services.AddHttpClient(ModgudLoginBroker.HttpClientName);
builder.Services.AddScoped<ModgudLoginBroker>();
builder.Services.AddTransient<IClaimsTransformation, ModgudClaimsTransformation>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(ctx => AdminCheck.IsAdmin(ctx.User, config)));
});

builder.Services.AddSingleton<IManifestService, ManifestService>();
builder.Services.AddSingleton<IUploadService, UploadService>();
builder.Services.AddSingleton<BasePathDetector>();

builder.Services.AddSingleton<IProductConfigService, MartenProductConfigService>();
builder.Services.AddHostedService<ProductConfigMigrationService>();

// Access log
if (config.AccessLog.Enabled)
{
    builder.Services.AddSingleton<AccessLogChannel>();
    builder.Services.AddHostedService<AccessLogPersistenceService>();
}

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
app.MapApiEndpoints(config);
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
