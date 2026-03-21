using Serilog.Events;

namespace Cocoar.Shelf;

public class ShelfOptions
{
    public string AppUrl { get; set; } = "http://0.0.0.0:8080";

    public string DocsRoot { get; set; } = "/data/docs";

    public string ConfigRoot { get; set; } = "/data/config";

    public string PathBase { get; set; } = "";

    public string VersionPattern { get; set; } = @"^v?\d+(\.\d+(\.\d+(-[\w.]+)?)?)?$";

    public string BasePlaceholder { get; set; } = "/__shelf__/";

    public string ApiKey { get; set; } = "";

    public long MaxUploadSizeBytes { get; set; } = 104_857_600; // 100 MB

    public ShelfLogging Logging { get; set; } = new();
}

public class ShelfLogging
{
    public Dictionary<string, LogEventLevel> LogLevels { get; set; } = new()
    {
        ["Default"] = LogEventLevel.Information,
        ["Microsoft.AspNetCore"] = LogEventLevel.Warning,
    };
}
