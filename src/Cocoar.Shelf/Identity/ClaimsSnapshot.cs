using System.Security.Claims;

namespace Cocoar.Shelf.Identity;

/// <summary>
/// Extracts the login claims snapshot persisted on <c>UserDocument</c> (Access Control v2): ALL claims
/// from the OIDC principal (multi-valued as arrays) plus the flattened modgud permissions as a
/// convenience field. No claim is filtered — the auto-membership predicate decides what matters.
/// </summary>
public static class ClaimsSnapshot
{
    public static (IReadOnlyDictionary<string, string[]> Claims, IReadOnlyList<string> Permissions) Extract(
        ClaimsPrincipal principal, string audience)
    {
        var claims = principal.Claims
            .GroupBy(c => c.Type, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Value).ToArray(), StringComparer.Ordinal);

        // Prefer already-flattened "permission" claims (a per-request transform may have run for the
        // test seam); otherwise parse the raw resource_access JSON stamped at OIDC sign-in.
        var permissions = principal.FindAll(ModgudClaimsTransformation.PermissionClaimType)
            .Select(c => c.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (permissions.Length == 0)
        {
            var raw = principal.FindFirst(ModgudClaimsTransformation.ResourceAccessClaimType)?.Value;
            permissions = ModgudClaimsTransformation.ParsePermissions(raw, audience);
        }

        return (claims, permissions);
    }
}
