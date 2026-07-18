using System.Text.Json;
using System.Text.Json.Serialization;
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
    /// <summary>The modgud host Shelf talks to (no realm path segment — modgud routes by Host
    /// header): OIDC authority for discovery, authorize, token and userinfo. May be the Shelf
    /// App-Origin subdomain (e.g. <c>https://shelf.auth.cocoar.dev</c>) for the branded login UI —
    /// modgud serves the full OAuth surface there and anchors the token issuer to the realm's
    /// canonical domain, which the handler picks up from discovery (ADR-0011).</summary>
    public string Issuer { get; set; } = "https://auth.cocoar.dev";

    /// <summary>The registered OAuth API name == the <c>resource_access</c> key carrying Shelf's
    /// roles/permissions.</summary>
    public string Audience { get; set; } = "shelf";

    /// <summary>Confidential OIDC client for the BFF login broker.</summary>
    public string? WebClientId { get; set; }

    /// <summary>Secret for <see cref="WebClientId"/> — env/user-secrets only, never in files.</summary>
    public string? WebClientSecret { get; set; }

    /// <summary>Permission that makes a principal admin (from modgud's shelf App catalog).</summary>
    public string AdminPermission { get; set; } = "shelf:admin";

    /// <summary>Email allowlist that is always admin — the no-lockout floor before modgud RBAC is
    /// seeded. Accepts a JSON array (config file) or a comma/semicolon-separated string
    /// (<c>Shelf__Modgud__Admins=a@x,b@y</c>) — env vars cannot express JSON arrays
    /// (<c>__0</c>-style keys build a <c>{"0":…}</c> object that fails to bind).</summary>
    [JsonConverter(typeof(StringArrayFromCsvConverter))]
    public string[] Admins { get; set; } = [];
}

/// <summary>Deserializes <c>string[]</c> from either a JSON string ("a, b; c" — split on comma or
/// semicolon, entries trimmed, empties dropped) or a JSON array of strings.</summary>
public sealed class StringArrayFromCsvConverter : JsonConverter<string[]>
{
    public override string[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return (reader.GetString() ?? "").Split([',', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException($"Expected a string or an array of strings, got {reader.TokenType}");

        var items = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected array of strings, got {reader.TokenType}");
            var value = reader.GetString()?.Trim();
            if (!string.IsNullOrEmpty(value))
                items.Add(value);
        }
        return [.. items];
    }

    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteStringValue(item);
        writer.WriteEndArray();
    }
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
