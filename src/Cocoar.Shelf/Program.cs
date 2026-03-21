using Cocoar.Configuration.AspNetCore;
using Cocoar.Configuration.Providers;
using Cocoar.Configuration.Reactive;
using Cocoar.Shelf;
using Cocoar.Shelf.Endpoints;
using Cocoar.Shelf.Middleware;
using Cocoar.Shelf.Services;
using System.Globalization;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, config) => config
        .ReadFrom.Configuration(context.Configuration));

    builder.AddCocoarConfiguration(c => c
        .UseConfiguration(rules => [
            rules.For<ShelfOptions>().FromFile("appsettings.json").Select("Shelf"),
            rules.For<ShelfOptions>().FromEnvironment("Shelf__")
        ]));

    builder.Services.AddSingleton<IManifestService, ManifestService>();
    builder.Services.AddSingleton<IProductConfigService, ProductConfigService>();
    builder.Services.AddSingleton<IUploadService, UploadService>();
    builder.Services.AddSingleton<BasePathDetector>();

    var app = builder.Build();

    var shelfOptions = app.Services.GetRequiredService<IReactiveConfig<ShelfOptions>>().CurrentValue;
    if (!string.IsNullOrEmpty(shelfOptions.PathBase))
        app.UsePathBase(shelfOptions.PathBase);

    if (shelfOptions.EnableLandingPage)
        app.MapGet("/", LandingPageEndpoint.Render);

    app.UseSerilogRequestLogging();

    app.MapApiEndpoints();
    app.UseMiddleware<DocsRoutingMiddleware>();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
