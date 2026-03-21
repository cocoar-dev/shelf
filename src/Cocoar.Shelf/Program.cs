using Cocoar.Configuration.AspNetCore;
using Cocoar.Configuration.DI.Extensions;
using Cocoar.Configuration.Providers;
using Cocoar.Shelf;
using Cocoar.Shelf.Endpoints;
using Cocoar.Shelf.Middleware;
using Cocoar.Shelf.Services;
using System.Globalization;
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
var config = configManager.GetConfig<ShelfOptions>();

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

    logConfig.WriteTo.Console(theme: AnsiConsoleTheme.Code);
});

builder.Services.AddSingleton<IManifestService, ManifestService>();
builder.Services.AddSingleton<IProductConfigService, ProductConfigService>();
builder.Services.AddSingleton<IUploadService, UploadService>();
builder.Services.AddSingleton<BasePathDetector>();

var app = builder.Build();

if (!string.IsNullOrEmpty(config.PathBase))
    app.UsePathBase(config.PathBase);

app.UseMiddleware<SpaFallbackMiddleware>();
app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.MapApiEndpoints();
app.UseMiddleware<DocsRoutingMiddleware>();

app.Run(config.AppUrl);

public partial class Program;
