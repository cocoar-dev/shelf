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
using Fido2NetLib;
using Marten;
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

// --- Database (Marten/PostgreSQL) ---
if (!string.IsNullOrEmpty(config.Database.ConnectionString))
{
    builder.Services.AddMarten(opts =>
    {
        opts.Connection(config.Database.ConnectionString);

        // Users
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

        // Auth challenges
        opts.Schema.For<EmailOtpChallenge>()
            .DatabaseSchemaName("shelf");
        opts.Schema.For<MagicLinkChallenge>()
            .DatabaseSchemaName("shelf")
            .Index(x => x.UserId);
        opts.Schema.For<StoredPasskeyCredential>()
            .DatabaseSchemaName("shelf")
            .Index(x => x.UserId);

        // Product config
        opts.Schema.For<ProductConfig>()
            .DatabaseSchemaName("shelf")
            .Identity(x => x.Name);
    })
    .UseLightweightSessions()
    .ApplyAllDatabaseChangesOnStartup();

    builder.Services.AddHttpClient("geoip");
    builder.Services.AddSingleton<GeoIpService>();
}

// --- Identity ---
if (!string.IsNullOrEmpty(config.Database.ConnectionString))
{
    builder.Services.AddIdentityCore<UserDocument>(opts =>
    {
        opts.Password.RequireDigit = true;
        opts.Password.RequireLowercase = true;
        opts.Password.RequireUppercase = true;
        opts.Password.RequireNonAlphanumeric = false;
        opts.Password.RequiredLength = 8;
        opts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        opts.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddSignInManager<AppSignInManager>()
    .AddDefaultTokenProviders()
    .AddUserStore<MartenUserStore>();

    builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
        .AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = "shelf.auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            };
        })
        .AddCookie(IdentityConstants.TwoFactorUserIdScheme, options =>
        {
            options.Cookie.Name = "shelf.2fa";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        });

    builder.Services.AddScoped<EmailOtpService>();
    builder.Services.AddScoped<MagicLinkService>();

    // Email service: configurable provider
    switch (config.Email.Provider.ToLowerInvariant())
    {
        case "smtp":
            builder.Services.AddSingleton<IEmailService, Cocoar.Shelf.Services.Email.SmtpEmailService>();
            break;
        case "postmark":
            builder.Services.AddSingleton<IEmailService, Cocoar.Shelf.Services.Email.PostmarkEmailService>();
            break;
        default:
            builder.Services.AddSingleton<IEmailService, LoggingEmailService>();
            break;
    }

    builder.Services.AddFido2(options =>
    {
        options.ServerDomain = "localhost";
        options.ServerName = "Shelf";
        options.Origins = new HashSet<string> { "http://localhost:8080", "https://localhost:8080" };
    });
}
else
{
    // Fallback: simple cookie auth without Identity (no DB)
    builder.Services.AddAuthentication()
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
}

builder.Services.AddAuthorization();

builder.Services.AddSingleton<IManifestService, ManifestService>();
builder.Services.AddSingleton<IUploadService, UploadService>();
builder.Services.AddSingleton<BasePathDetector>();

if (!string.IsNullOrEmpty(config.Database.ConnectionString))
{
    builder.Services.AddSingleton<IProductConfigService, MartenProductConfigService>();
    builder.Services.AddHostedService<ProductConfigMigrationService>();
}
else
{
    builder.Services.AddSingleton<IProductConfigService, ProductConfigService>();
}

// Access log (requires DB)
if (!string.IsNullOrEmpty(config.Database.ConnectionString) && config.AccessLog.Enabled)
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
