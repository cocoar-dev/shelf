using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cocoar.Shelf.Identity;

/// <summary>
/// Web BFF login broker: the SPA keeps its own Shelf-branded login UI and posts the email/code to
/// <b>this</b> backend, which then talks to modgud's <b>native grants</b> server-to-server. On a
/// successful code redeem the backend establishes the normal <c>shelf.auth</c> cookie session keyed
/// to the modgud <c>sub</c>.
///
/// <para>The browser never reaches modgud (no redirect to a hosted modgud page, no CORS): the OTP
/// request and the <c>/connect/token</c> redeem are both server-to-server. modgud's tokens are used
/// only to read the identity at login (<c>sub</c>/email/name via <c>/connect/userinfo</c>) and then
/// discarded — the cookie <i>is</i> the web session.</para>
///
/// <para>The native OTP <i>request</i> is sent to the App subdomain (<c>Modgud:AuthBase</c>) so modgud
/// host-resolves the right Application + its self-registration posture; the token redeem goes to the
/// bare issuer (<c>Modgud:Issuer</c>) and carries the confidential web client_id. In dev both hosts
/// are the same, so <c>AuthBase</c> may stay unset.</para>
/// </summary>
public sealed partial class ModgudLoginBroker(
    IHttpClientFactory http,
    ShelfOptions options,
    ILogger<ModgudLoginBroker> log)
{
    public const string HttpClientName = "modgud-login";

    private string Issuer => Trimmed(options.Modgud.Issuer) ?? "https://auth.cocoar.dev";
    private string AuthBase => Trimmed(options.Modgud.AuthBase) ?? Issuer;
    private string? ClientId => Trimmed(options.Modgud.WebClientId);
    private string? ClientSecret => Trimmed(options.Modgud.WebClientSecret);

    // roles/permissions/<audience> pull modgud's resource_access[<audience>] (RBAC) into userinfo so
    // the cookie session can carry admin claims. The web client must be granted all of these in
    // modgud — requesting an ungranted scope makes the token request fail (invalid_request).
    private string LoginScope => $"openid email profile roles permissions {options.Modgud.Audience}".Trim();

    private static string? Trimmed(string? value) =>
        value?.Trim().TrimEnd('/') is { Length: > 0 } v ? v : null;

    /// <summary>Asks modgud to email a login code. Anti-enumeration is modgud's job (uniform
    /// response); the caller relays a generic message regardless of the outcome.</summary>
    public async Task RequestOtpAsync(string email, CancellationToken ct)
    {
        var client = http.CreateClient(HttpClientName);
        using var res = await client.PostAsJsonAsync(
            $"{AuthBase}/api/account/native/otp/request", new { Email = email }, ct);
        if (!res.IsSuccessStatusCode)
            LogOtpRequestFailed(log, (int)res.StatusCode);
    }

    /// <summary>Redeems an emailed code at modgud's <c>urn:cocoar:otp</c> grant and returns the
    /// federated identity, or null if the code is invalid / modgud is unreachable / the web client
    /// isn't configured. The minted tokens are discarded — the cookie is the session.</summary>
    public async Task<BrokerIdentity?> VerifyOtpAsync(string email, string code, CancellationToken ct)
    {
        if (ClientId is null)
        {
            LogClientNotConfigured(log);
            return null;
        }

        var client = http.CreateClient(HttpClientName);
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "urn:cocoar:otp",
            ["username"] = email,
            ["otp_code"] = code,
            ["client_id"] = ClientId,
            ["scope"] = LoginScope,
        };
        if (ClientSecret is not null) form["client_secret"] = ClientSecret;

        try
        {
            using var res = await client.PostAsync($"{Issuer}/connect/token", new FormUrlEncodedContent(form), ct);
            if (!res.IsSuccessStatusCode) return null; // invalid_grant on a wrong/expired code, etc.

            var token = ParseAccessToken(await res.Content.ReadAsStringAsync(ct));
            if (token is null) return null;

            return await FetchIdentityAsync(client, token, ct);
        }
        catch (HttpRequestException ex)
        {
            // An unreachable IdP must surface as a failed login, not a 500.
            LogModgudUnreachable(log, ex);
            return null;
        }
    }

    private static string? ParseAccessToken(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("access_token", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }

    // Read the identity (sub/email/name + resource_access) off /connect/userinfo — the canonical,
    // fully populated source (surfaces resource_access uniformly).
    private async Task<BrokerIdentity?> FetchIdentityAsync(HttpClient client, string token, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{Issuer}/connect/userinfo");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await client.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        string? Get(params string[] keys) => keys
            .Select(k => root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null)
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        if (Get("sub") is not { } sub || !Guid.TryParse(sub, out var uid)) return null;

        // Capture the raw resource_access JSON object (RBAC) so otp/verify can stamp it onto the
        // cookie identity; the ClaimsTransformation then flattens it into role/permission claims.
        string? resourceAccess = root.TryGetProperty("resource_access", out var ra) && ra.ValueKind == JsonValueKind.Object
            ? ra.GetRawText()
            : null;
        return new BrokerIdentity(uid, Get("email"), Get("name", "preferred_username", "given_name"), resourceAccess);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "modgud native OTP request returned {Status}")]
    private static partial void LogOtpRequestFailed(ILogger logger, int status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Modgud WebClientId is not configured — the web login broker cannot redeem codes")]
    private static partial void LogClientNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "modgud is unreachable — login rejected")]
    private static partial void LogModgudUnreachable(ILogger logger, Exception ex);
}

public record BrokerIdentity(Guid Sub, string? Email, string? Name, string? ResourceAccess = null);
