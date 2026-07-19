using System.Security.Claims;

namespace Cocoar.Shelf.Services.Access;

/// <summary>
/// Runs at each login (real OIDC callback and the test sign-in seam): refreshes the user's persisted
/// claims snapshot from the freshly-authenticated principal, then recomputes that user's auto-group
/// membership — the bridge that maps modgud claims to Shelf group grants.
/// </summary>
public interface ILoginAccessProcessor
{
    Task RefreshSnapshotAndRecalculateAsync(
        Guid userId, ClaimsPrincipal principal, CancellationToken ct = default);
}
