using Serilog.Events;

namespace Cocoar.Shelf;

public class ShelfOptions
{
    public string AppUrl { get; set; } = "http://0.0.0.0:8080";

    public string DocsRoot { get; set; } = "/data/docs";

    public string ConfigRoot { get; set; } = "/data/config";

    public string PathBase { get; set; } = "";

    public string VersionPattern { get; set; } = @"^v?\d+(\.\d+(\.\d+(-[\w.-]+)?)?)?$";

    public string BasePlaceholder { get; set; } = "/__shelf__/";

    public string ApiKey { get; set; } = "";

    public long MaxUploadSizeBytes { get; set; } = 104_857_600; // 100 MB

    public DatabaseOptions Database { get; set; } = new();

    public EmailOptions Email { get; set; } = new();

    public AccessLogOptions AccessLog { get; set; } = new();

    public ShelfLogging Logging { get; set; } = new();
}

public class DatabaseOptions
{
    public string? ConnectionString { get; set; }
}

public class EmailOptions
{
    public string Provider { get; set; } = "logging"; // "logging", "smtp", "postmark"

    public SmtpOptions Smtp { get; set; } = new();

    public PostmarkOptions Postmark { get; set; } = new();
}

public class SmtpOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 25;

    public bool UseSsl { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "noreply@shelf.local";

    public string? FromName { get; set; }
}

public class PostmarkOptions
{
    public string ServerToken { get; set; } = "";

    public string FromAddress { get; set; } = "";

    public string? FromName { get; set; }

    public string? MessageStream { get; set; }
}

public class AccessLogOptions
{
    public bool Enabled { get; set; }

    public int RetentionDays { get; set; } = 90;
}

public class ShelfLogging
{
    public Dictionary<string, LogEventLevel> LogLevels { get; set; } = new()
    {
        ["Default"] = LogEventLevel.Information,
        ["Microsoft.AspNetCore"] = LogEventLevel.Warning,
    };
}
