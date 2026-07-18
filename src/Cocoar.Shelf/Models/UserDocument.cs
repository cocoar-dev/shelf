namespace Cocoar.Shelf.Models;

/// <summary>
/// Thin local mirror of a federated modgud identity. <see cref="Id"/> == the modgud <c>sub</c>;
/// credentials, verification and 2FA live in modgud. This doc exists to hang app-specific user
/// state off and to carry the cookie session (security stamp) via ASP.NET Identity.
/// </summary>
public class UserDocument
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = "";

    public string? NormalizedUserName { get; set; }

    public string? Email { get; set; }

    public string? NormalizedEmail { get; set; }

    public string? DisplayName { get; set; }

    public string? SecurityStamp { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Snapshot of ALL claims from the OIDC principal, refreshed on every login (Access Control v2).
    /// Multi-valued claims are stored as arrays. This is the input a group's auto-membership JsEval
    /// predicate runs against — no claim is filtered out, the predicate decides what matters. Scripts
    /// never see tokens, only this persisted map.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Claims { get; set; } =
        new Dictionary<string, string[]>();

    /// <summary>Flattened modgud <c>resource_access[shelf]</c> permissions (convenience field derived
    /// from the claims snapshot at login), so a predicate can read <c>user.permissions</c> directly.</summary>
    public IReadOnlyList<string> Permissions { get; set; } = [];

    /// <summary>When the <see cref="Claims"/> snapshot was last refreshed (i.e. last login). Null for
    /// users provisioned before Access Control v2 who have not logged in since.</summary>
    public DateTimeOffset? ClaimsUpdatedAt { get; set; }
}
