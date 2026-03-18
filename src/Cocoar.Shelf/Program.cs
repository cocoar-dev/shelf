using Cocoar.Shelf;
using Cocoar.Shelf.Middleware;
using Cocoar.Shelf.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ShelfOptions>(builder.Configuration.GetSection("Shelf"));
builder.Services.AddSingleton<IManifestService, ManifestService>();
builder.Services.AddSingleton<BasePathDetector>();

var app = builder.Build();

app.UseMiddleware<DocsRoutingMiddleware>();

app.Run();
