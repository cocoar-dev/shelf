using System.Security.Claims;
using Cocoar.Shelf.Identity;
using Cocoar.Shelf.Services;

namespace Cocoar.Shelf.Services.Access;

/// <summary>
/// Per-request (scoped) grant resolver. Effective membership of a user in a group is
/// <c>email ∈ MemberEmails</c> (explicit, stageable pre-login) ∪ <c>id ∈ AutoMemberUserIds</c>
/// (materialized auto-membership) — a cheap in-memory check over the loaded groups, never JsEval.
/// </summary>
public sealed class AccessResolver(IGroupService groups, ShelfOptions options) : IAccessResolver
{
    private AccessGrants? _cached;

    public async Task<bool> IsAdminAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        // Fast path: token permission or allowlist admin needs no group lookup.
        if (AdminCheck.IsAdmin(principal, options))
            return true;
        return (await ResolveAsync(principal, ct)).IsAdmin;
    }

    public async Task<AccessGrants> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        if (_cached is not null)
            return _cached;

        if (principal.Identity?.IsAuthenticated != true)
            return _cached = AccessGrants.None;

        var isAdmin = AdminCheck.IsAdmin(principal, options);
        var email = AdminCheck.Email(principal);
        var userId = CurrentUser.Id(principal);
        var groupIds = new HashSet<Guid>();

        foreach (var group in await groups.GetAllAsync())
        {
            var isMember =
                (email is not null && group.MemberEmails.Any(m => string.Equals(m, email, StringComparison.OrdinalIgnoreCase)))
                || (userId is { } uid && group.AutoMemberUserIds.Contains(uid));
            if (!isMember)
                continue;

            groupIds.Add(group.Id);
            if (group.IsAdminGroup)
                isAdmin = true;
        }

        return _cached = new AccessGrants(isAdmin, groupIds, email);
    }
}
