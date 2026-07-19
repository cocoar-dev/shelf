namespace Cocoar.Shelf.Services.Access;

/// <summary>
/// Recomputes materialized auto-membership (<c>Group.AutoMemberUserIds</c>) by evaluating each auto
/// group's JsEval predicate over the persisted user claims snapshots. In-memory over Marten documents
/// (no SQL translation, no event sourcing) — fine at Shelf's user counts. Three triggers, per the
/// concept: at a user's login, on group save, and on manual admin recalc.
/// </summary>
public interface IGroupMembershipRecalculator
{
    /// <summary>Login/user trigger: recompute only <paramref name="userId"/>'s membership across all
    /// auto groups, adding or removing that one user. Persists only the groups that changed.</summary>
    Task RecalculateForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Group-save trigger: recompute one group's members across all users.</summary>
    Task RecalculateGroupAsync(Guid groupId, CancellationToken ct = default);

    /// <summary>Manual trigger: recompute every group across all users.</summary>
    Task RecalculateAllAsync(CancellationToken ct = default);
}
