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

    public ModgudOptions Modgud { get; set; } = new();

    public AccessLogOptions AccessLog { get; set; } = new();

    public ShelfLogging Logging { get; set; } = new();

    /// <summary>Maps the test-only sign-in seam (<c>/_api/test/signin</c>). Set ONLY by the
    /// integration test fixture — never in dev-default or production.</summary>
    public bool TestAuth { get; set; }
}

public class DatabaseOptions
{
    public string? ConnectionString { get; set; }
}

/// <summary>
/// Federation to the modgud identity provider. Login is brokered server-to-server (native OTP
/// grant); modgud owns all credentials, Shelf keeps only a thin local user layer.
/// </summary>
public class ModgudOptions
{
    /// <summary>Realm host root (no realm path segment — modgud routes by Host header). Also the
    /// JWKS authority and base for <c>/connect/token</c> + <c>/connect/userinfo</c>.</summary>
    public string Issuer { get; set; } = "https://auth.cocoar.dev";

    /// <summary>The registered OAuth API name == the <c>resource_access</c> key carrying Shelf's
    /// roles/permissions.</summary>
    public string Audience { get; set; } = "shelf";

    /// <summary>App subdomain the native OTP request goes to, so modgud host-resolves the Shelf
    /// Application + its self-registration posture. Unset = use <see cref="Issuer"/> (dev).</summary>
    public string? AuthBase { get; set; }

    /// <summary>Confidential OIDC client for the BFF login broker.</summary>
    public string? WebClientId { get; set; }

    /// <summary>Secret for <see cref="WebClientId"/> — env/user-secrets only, never in files.</summary>
    public string? WebClientSecret { get; set; }

    /// <summary>Permission that makes a principal admin (from modgud's shelf App catalog).</summary>
    public string AdminPermission { get; set; } = "shelf:admin";

    /// <summary>Email allowlist that is always admin — the no-lockout floor before modgud RBAC is
    /// seeded.</summary>
    public string[] Admins { get; set; } = [];
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
