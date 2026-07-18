using System.Security.Claims;

namespace Cocoar.Shelf.Services.Access;

/// <summary>
/// The effective grants of a principal, computed per request against the DB (Access Control v2):
/// whether they are a Shelf admin and which restricted products they may read. Cheap to consult
/// repeatedly — resolution is memoized per request.
/// </summary>
public sealed record AccessGrants(bool IsAdmin, IReadOnlySet<string> ReadableProducts)
{
    /// <summary>True if the principal may read <paramref name="product"/> (admins read everything).</summary>
    public bool CanRead(string product) => IsAdmin || ReadableProducts.Contains(product);

    public static readonly AccessGrants None =
        new(false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}

/// <summary>
/// Resolves a principal's effective access from group grants, the token permission and the admin
/// allowlist — the single per-request authority the enforcement points (docs middleware, product
/// API, admin policy) consult. Revocation takes effect immediately (next request), not at next login.
/// </summary>
public interface IAccessResolver
{
    Task<AccessGrants> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct = default);

    /// <summary>Admin check with a no-DB fast path (token permission / allowlist) before group lookup.</summary>
    Task<bool> IsAdminAsync(ClaimsPrincipal principal, CancellationToken ct = default);
}
