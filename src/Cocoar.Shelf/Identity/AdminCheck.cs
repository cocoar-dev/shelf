using System.Security.Claims;

namespace Cocoar.Shelf.Identity;

/// <summary>
/// Admin detection shared by the <c>Admin</c> authorization policy and <c>GET /_api/auth/me</c>.
///
/// <para>A principal is admin if it carries the configured permission claim
/// (<see cref="ModgudOptions.AdminPermission"/>, default <c>shelf:admin</c> — the permission modgud
/// seeds in the shelf App catalog, flattened from <c>resource_access[shelf]</c> by
/// <see cref="ModgudClaimsTransformation"/>) OR its email is in the
/// <see cref="ModgudOptions.Admins"/> allowlist (case-insensitive). The allowlist is the
/// no-lockout floor: it makes an operator admin before modgud RBAC is seeded.</para>
///
/// <para>Fail-closed: empty allowlist + no permission claim ⇒ not admin.</para>
/// </summary>
public static class AdminCheck
{
    public static bool IsAdmin(ClaimsPrincipal? principal, ShelfOptions options)
    {
        if (principal?.Identity?.IsAuthenticated != true) return false;

        var required = options.Modgud.AdminPermission.Trim();
        if (required.Length > 0 && principal.FindAll(ModgudClaimsTransformation.PermissionClaimType)
                .Any(c => string.Equals(c.Value, required, StringComparison.Ordinal)))
            return true;

        return IsAllowlisted(Email(principal), options);
    }

    /// <summary>True if <paramref name="email"/> is in the admin allowlist (case-insensitive).</summary>
    public static bool IsAllowlisted(string? email, ShelfOptions options)
        => !string.IsNullOrWhiteSpace(email)
           && options.Modgud.Admins.Any(a => string.Equals(a.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The principal's flattened <c>"permission"</c> claim values (deduped, stable order).</summary>
    public static string[] Permissions(ClaimsPrincipal? principal)
        => principal is null
            ? []
            : principal.FindAll(ModgudClaimsTransformation.PermissionClaimType)
                .Select(c => c.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

    // The cookie principal carries ClaimTypes.Email (Identity's claims factory reads the email store).
    public static string? Email(ClaimsPrincipal principal)
        => principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.FindFirst("email")?.Value;
}
