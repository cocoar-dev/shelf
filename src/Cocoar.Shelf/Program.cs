using Cocoar.Shelf;
using Cocoar.Shelf.Endpoints;
using Cocoar.Shelf.Middleware;
using Cocoar.Shelf.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ShelfOptions>(builder.Configuration.GetSection("Shelf"));
builder.Services.AddSingleton<IManifestService, ManifestService>();
builder.Services.AddSingleton<IProductConfigService, ProductConfigService>();
builder.Services.AddSingleton<IUploadService, UploadService>();
builder.Services.AddSingleton<BasePathDetector>();

var app = builder.Build();

var shelfOptions = app.Services.GetRequiredService<IOptions<ShelfOptions>>().Value;
if (!string.IsNullOrEmpty(shelfOptions.PathBase))
    app.UsePathBase(shelfOptions.PathBase);

if (shelfOptions.EnableLandingPage)
    app.MapGet("/", LandingPageEndpoint.Render);

app.MapApiEndpoints();
app.UseMiddleware<DocsRoutingMiddleware>();

app.Run();
