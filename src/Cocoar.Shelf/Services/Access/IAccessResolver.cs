using System.Security.Claims;
using Cocoar.Shelf.Models;

namespace Cocoar.Shelf.Services.Access;

/// <summary>
/// The effective grant context of a principal, computed per request against the DB (Access Control
/// v2): whether they are a Shelf admin, and the identity (group memberships + email) needed to test a
/// product's read-principals. Cheap to consult repeatedly — resolution is memoized per request.
/// </summary>
public sealed record AccessGrants(bool IsAdmin, IReadOnlySet<Guid> GroupIds, string? Email)
{
    /// <summary>True if the principal may read <paramref name="product"/>: admin, or listed among the
    /// product's read-principals (a group they belong to, or their own email).</summary>
    public bool CanRead(ProductConfig product)
    {
        if (IsAdmin)
            return true;

        return product.ReadPrincipals.Any(p => p.Kind switch
        {
            PrincipalKind.Group => Guid.TryParse(p.Id, out var groupId) && GroupIds.Contains(groupId),
            PrincipalKind.User => Email is not null && string.Equals(p.Id, Email, StringComparison.OrdinalIgnoreCase),
            _ => false,
        });
    }

    public static readonly AccessGrants None = new(false, new HashSet<Guid>(), null);
}

/// <summary>
/// Resolves a principal's grant context from group memberships, the token permission and the admin
/// allowlist — the single per-request authority the enforcement points (docs middleware, product
/// API, admin policy) consult. Revocation takes effect immediately (next request), not at next login.
/// </summary>
public interface IAccessResolver
{
    Task<AccessGrants> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct = default);

    /// <summary>Admin check with a no-DB fast path (token permission / allowlist) before group lookup.</summary>
    Task<bool> IsAdminAsync(ClaimsPrincipal principal, CancellationToken ct = default);
}
