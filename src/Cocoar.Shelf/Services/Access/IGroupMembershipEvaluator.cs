namespace Cocoar.Shelf.Services.Access;

/// <summary>Outcome of evaluating a group predicate against one user.</summary>
/// <param name="Matched">Whether the predicate matched (always false when <paramref name="Error"/> is set).</param>
/// <param name="Error">The failure message (syntax/throw/timeout/non-boolean), or null on success.</param>
public readonly record struct GroupPredicateResult(bool Matched, string? Error);

/// <summary>Evaluates a group's JsEval auto-membership predicate against one user (fail-closed).</summary>
public interface IGroupMembershipEvaluator
{
    /// <summary>Evaluates <paramref name="script"/> (a JS boolean expression over the global
    /// <c>user</c>) against <paramref name="user"/>, surfacing any error for diagnostics (the admin
    /// dry-run and a group's last-error). A failure never matches.</summary>
    GroupPredicateResult Evaluate(string script, GroupMembershipContext user);

    /// <summary>Convenience for the recompute hot path: <see cref="Evaluate"/>'s match result,
    /// discarding the error. Any error — syntax, throw, timeout, non-boolean — returns false.</summary>
    bool IsMember(string script, GroupMembershipContext user) => Evaluate(script, user).Matched;
}
