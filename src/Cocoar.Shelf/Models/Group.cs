namespace Cocoar.Shelf.Models;

/// <summary>How a <see cref="Group"/>'s effective membership is determined.</summary>
public enum MembershipMode
{
    /// <summary>Membership is the explicit <see cref="Group.MemberEmails"/> list only.</summary>
    Manual = 0,

    /// <summary>Membership is computed by evaluating <see cref="Group.MembershipScript"/> (a JsEval
    /// predicate over each user's login claims snapshot) in addition to the explicit list.</summary>
    Auto = 1,
}

/// <summary>
/// A permission carrier (Access Control v2). Groups are the only place product-read access and Shelf
/// adminship are granted — Shelf has exactly two grantable things, so grants sit directly on the group
/// (no PermissionRole catalog). Identity still lives entirely in modgud; a group never holds credentials.
///
/// <para>Membership is either an explicit email list (<see cref="MembershipMode.Manual"/>) or, for
/// <see cref="MembershipMode.Auto"/>, additionally computed from a JsEval predicate over the user's
/// persisted login claims snapshot. Email-based explicit membership lets grants be staged before a
/// user's first login (the local <see cref="UserDocument"/> is JIT-provisioned).</para>
/// </summary>
public class Group : IPrincipal
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    PrincipalKind IPrincipal.PrincipalKind => PrincipalKind.Group;
    string IPrincipal.PrincipalId => Id.ToString();
    string IPrincipal.PrincipalDisplayName => Name;

    public MembershipMode MembershipMode { get; set; } = MembershipMode.Manual;

    /// <summary>Explicit members, by email (case-insensitive). Applies in both membership modes;
    /// email-based so grants can be staged before the user's first login.</summary>
    public IReadOnlyList<string> MemberEmails { get; set; } = [];

    /// <summary><see cref="MembershipMode.Auto"/> only: a JsEval predicate over the user claims
    /// snapshot returning a bool. Ignored in <see cref="MembershipMode.Manual"/> mode.</summary>
    public string? MembershipScript { get; set; }

    /// <summary>
    /// Materialized ids of users matched by <see cref="MembershipScript"/> in <see
    /// cref="MembershipMode.Auto"/>, recomputed on triggers (login of a user, group save, manual
    /// recalc). Not admin-editable. Kept separate from <see cref="MemberEmails"/> so explicit grants
    /// (email, stageable before first login) and auto grants (user id, materialized) don't clobber
    /// each other. Effective membership = email ∈ MemberEmails ∪ id ∈ AutoMemberUserIds.
    /// </summary>
    public IReadOnlyList<Guid> AutoMemberUserIds { get; set; } = [];

    /// <summary>Last auto-membership recompute error (surfaced in the admin UI); null when the last
    /// evaluation succeeded. Only meaningful in <see cref="MembershipMode.Auto"/>.</summary>
    public string? MembershipLastError { get; set; }

    /// <summary>When true, members of this group are Shelf admins.</summary>
    public bool IsAdminGroup { get; set; }

    /// <summary>Soft-delete flag; deleted groups grant nothing and are hidden from the admin UI.</summary>
    public bool IsDeleted { get; set; }
}
