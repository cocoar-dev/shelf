using System.Globalization;
using System.Security.Claims;
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
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
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

    // Global runtime settings (single document)
    opts.Schema.For<ShelfSettings>()
        .DatabaseSchemaName("shelf");
})
.UseLightweightSessions()
.ApplyAllDatabaseChangesOnStartup();

builder.Services.AddHttpClient("geoip");
builder.Services.AddSingleton<GeoIpService>();

// --- Identity + modgud federation ---
// modgud (external IdP) owns all credentials; login is the standard OIDC authorization-code flow
// (browser redirect to modgud → callback → cookie), so a modgud browser session gives SSO across
// the Cocoar apps. ASP.NET Identity plumbing stays ONLY to carry the cookie (SecurityStamp
// pipeline) and as the integration tests' sign-in seam — it is not a product login.
builder.Services.AddIdentityCore<UserDocument>()
    .AddSignInManager()
    .AddUserStore<MartenUserStore>();

// AddIdentityCore doesn't register the stamp validator the cookie's ValidatePrincipal hook uses.
builder.Services.AddScoped<ISecurityStampValidator, SecurityStampValidator<UserDocument>>();

// TestAuth (integration-test fixture only) replaces login with the test seam — never register
// the OIDC handler there: it would fetch discovery from the configured issuer on first use.
var modgudConfigured = !config.TestAuth
    && !string.IsNullOrWhiteSpace(config.Modgud.WebClientId)
    && !string.IsNullOrWhiteSpace(config.Modgud.WebClientSecret);

var authBuilder = builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "shelf.auth";
        options.Cookie.HttpOnly = true;
        // Lax (not Strict): the post-login navigation arrives from modgud's redirect — with
        // Strict the fresh session cookie would be withheld on that cross-site navigation.
        options.Cookie.SameSite = SameSiteMode.Lax;
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

if (modgudConfigured)
{
    authBuilder.AddOpenIdConnect(options =>
    {
        options.Authority = config.Modgud.Issuer;        // host root — modgud routes by Host header
        options.ClientId = config.Modgud.WebClientId;
        options.ClientSecret = config.Modgud.WebClientSecret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;                          // S256 (modgud rejects plain)
        options.RequireHttpsMetadata =
            config.Modgud.Issuer.StartsWith("https", StringComparison.OrdinalIgnoreCase);
        // Kept in the cookie's auth properties: RP-initiated logout needs the id_token as
        // id_token_hint (modgud rejects end-session without it). Shelf never calls modgud
        // with the access token — the cookie is the session.
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";

        options.Scope.Clear();
        foreach (var s in new[] { "openid", "profile", "email", "roles", "permissions", config.Modgud.Audience })
            options.Scope.Add(s);

        // Shelf serves plain http on the LAN (staging) and https behind the proxy (prod). The
        // default form_post callback needs SameSite=None;Secure correlation cookies, which
        // browsers drop over http — use the GET (query) callback + Lax cookies everywhere.
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.NonceCookie.SameSite = SameSiteMode.Lax;
        options.Events.OnRedirectToIdentityProvider = ctx =>
        {
            ctx.ProtocolMessage.ResponseMode = OpenIdConnectResponseMode.Query;
            return Task.CompletedTask;
        };

        // modgud's resource_access (RBAC) rarely rides the id_token — it arrives on the UserInfo
        // response as a nested JSON object, which the handler does NOT map into claims by
        // default. Capture the raw JSON; ModgudClaimsTransformation flattens it per request.
        options.Events.OnUserInformationReceived = ctx =>
        {
            if (ctx.Principal?.Identity is ClaimsIdentity identity &&
                ctx.User.RootElement.TryGetProperty("resource_access", out var ra) &&
                !identity.HasClaim(c => c.Type == ModgudClaimsTransformation.ResourceAccessClaimType))
            {
                identity.AddClaim(new Claim(ModgudClaimsTransformation.ResourceAccessClaimType, ra.GetRawText()));
            }
            return Task.CompletedTask;
        };

        // JIT-provision the thin local user, then swap the transient OIDC principal for the
        // Identity cookie principal (which carries the SecurityStamp) before the handler signs
        // it into the application cookie.
        options.Events.OnTicketReceived = async ctx =>
        {
            var services = ctx.HttpContext.RequestServices;
            var users = services.GetRequiredService<UserManager<UserDocument>>();
            var signIn = services.GetRequiredService<SignInManager<UserDocument>>();

            var oidc = ctx.Principal!;
            if (!Guid.TryParse(oidc.FindFirst("sub")?.Value, out var sub))
            {
                ctx.Fail("modgud ticket without a usable sub claim");
                return;
            }

            var user = await ModgudUserProvisioning.EnsureLocalUserAsync(
                users, sub, oidc.FindFirst("email")?.Value, oidc.FindFirst("name")?.Value);
            if (!user.IsActive)
            {
                ctx.Fail("account is deactivated");
                return;
            }

            ctx.Principal = await ModgudUserProvisioning.CreatePrincipalWithRbacAsync(
                signIn, user, oidc.FindFirst(ModgudClaimsTransformation.ResourceAccessClaimType)?.Value);
            ctx.Properties!.IsPersistent = true;
        };
    });
}

builder.Services.AddTransient<IClaimsTransformation, ModgudClaimsTransformation>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(ctx => AdminCheck.IsAdmin(ctx.User, config)));
});

// Behind a TLS-terminating reverse proxy: honor X-Forwarded-Proto/Host so the app builds correct
// absolute URLs — critically the OIDC redirect_uri, which must come out as https://<host>/signin-oidc
// (not the internal http the app sees). Also puts the real visitor IP into the access log.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddSingleton<IManifestService, ManifestService>();
builder.Services.AddSingleton<IUploadService, UploadService>();
builder.Services.AddSingleton<BasePathDetector>();
builder.Services.AddSingleton<ISettingsService, SettingsService>();

builder.Services.AddSingleton<IProductConfigService, MartenProductConfigService>();
builder.Services.AddHostedService<ProductConfigMigrationService>();

// Access log
if (config.AccessLog.Enabled)
{
    builder.Services.AddSingleton<AccessLogChannel>();
    builder.Services.AddHostedService<AccessLogPersistenceService>();
}

var app = builder.Build();

// First: apply X-Forwarded-* from the reverse proxy, so scheme/host are correct for everything
// downstream (OIDC redirect_uri, generated links, access-log IPs).
app.UseForwardedHeaders();

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
app.MapAuthPages(modgudConfigured);
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
