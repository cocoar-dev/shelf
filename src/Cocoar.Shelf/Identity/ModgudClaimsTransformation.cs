using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Cocoar.Shelf.Identity;

/// <summary>
/// Pre-request claims-transformation that flattens <c>resource_access[Modgud:Audience]</c> from the
/// principal's claims into flat <see cref="ClaimTypes.Role"/> and <c>"permission"</c> claims so the
/// <c>Admin</c> policy works without per-endpoint plumbing.
///
/// <para>In-repo port of the amzettel/modgud client transformation. Source of the data: a
/// <c>resource_access</c> string-typed claim whose value is the raw JSON object, stamped onto the
/// cookie identity at OIDC sign-in (captured from the UserInfo response). The IdP pre-expands bypass
/// tiers (<c>realm:admin</c> / <c>&lt;r&gt;:admin</c>) before emission, so this is a pure flatten —
/// exact-match against the <c>"permission"</c> claims downstream is sufficient. Idempotent: a second
/// pass on the same identity does not duplicate claims.</para>
/// </summary>
public sealed class ModgudClaimsTransformation : IClaimsTransformation
{
    /// <summary>Claim type for permission strings (<c>"&lt;resource&gt;:&lt;action&gt;"</c>).</summary>
    public const string PermissionClaimType = "permission";

    /// <summary>The standard OIDC/Keycloak claim that nests per-RS authz info.</summary>
    public const string ResourceAccessClaimType = "resource_access";

    private readonly string _audience;

    public ModgudClaimsTransformation(ShelfOptions options)
    {
        _audience = options.Modgud.Audience.Trim();
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // No audience configured → nothing to look up (admin still resolves via the Admins allowlist).
        if (string.IsNullOrWhiteSpace(_audience))
            return Task.FromResult(principal);

        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return Task.FromResult(principal);

        var raw = identity.FindFirst(ResourceAccessClaimType)?.Value;
        if (string.IsNullOrEmpty(raw))
            return Task.FromResult(principal);

        if (!TryParseJson(raw, out var resourceAccess) ||
            resourceAccess.ValueKind != JsonValueKind.Object)
            return Task.FromResult(principal);

        if (!resourceAccess.TryGetProperty(_audience, out var audienceBlock) ||
            audienceBlock.ValueKind != JsonValueKind.Object)
            return Task.FromResult(principal);

        FlattenStringArray(identity, audienceBlock, "roles", ClaimTypes.Role);
        FlattenStringArray(identity, audienceBlock, "permissions", PermissionClaimType);

        return Task.FromResult(principal);
    }

    private static void FlattenStringArray(
        ClaimsIdentity identity, JsonElement audienceBlock, string property, string claimType)
    {
        if (!audienceBlock.TryGetProperty(property, out var array) ||
            array.ValueKind != JsonValueKind.Array)
            return;

        var existing = new HashSet<string>(
            identity.FindAll(claimType).Select(c => c.Value),
            StringComparer.Ordinal);

        foreach (var element in array.EnumerateArray())
        {
            var value = element.GetString();
            if (string.IsNullOrEmpty(value) || !existing.Add(value)) continue;
            identity.AddClaim(new Claim(claimType, value));
        }
    }

    private static bool TryParseJson(string raw, out JsonElement element)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            element = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            element = default;
            return false;
        }
    }
}
