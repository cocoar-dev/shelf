using System.Security.Claims;
using Cocoar.Shelf.Identity;
using Cocoar.Shelf.Models;
using Marten;

namespace Cocoar.Shelf.Services.Access;

public sealed class LoginAccessProcessor(
    IDocumentStore store,
    IGroupMembershipRecalculator recalculator,
    ShelfOptions options) : ILoginAccessProcessor
{
    public async Task RefreshSnapshotAndRecalculateAsync(
        Guid userId, ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var (claims, permissions) = ClaimsSnapshot.Extract(principal, options.Modgud.Audience);

        await using (var session = store.LightweightSession())
        {
            var user = await session.LoadAsync<UserDocument>(userId, ct);
            if (user is null)
                return;

            user.Claims = claims;
            user.Permissions = permissions;
            user.ClaimsUpdatedAt = DateTimeOffset.UtcNow;
            session.Store(user);
            await session.SaveChangesAsync(ct);
        }

        // Immediately reflect the refreshed snapshot in this user's auto-group membership.
        await recalculator.RecalculateForUserAsync(userId, ct);
    }
}
