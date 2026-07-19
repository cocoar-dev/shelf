using Cocoar.Shelf.Models;

namespace Cocoar.Shelf.Services.Access;

/// <summary>
/// The read-only user view a group's auto-membership predicate runs against. Mirrors the persisted
/// login claims snapshot; exposed to the sandboxed script as the global <c>user</c> with lowercase
/// members: <c>user.email</c>, <c>user.permissions</c> (string[]), <c>user.claims</c> (map of claim
/// type → string | string[]). Scripts never see tokens.
/// </summary>
public sealed record GroupMembershipContext(
    string? Email,
    IReadOnlyList<string> Permissions,
    IReadOnlyDictionary<string, string[]> Claims)
{
    public static GroupMembershipContext FromUser(UserDocument user) =>
        new(user.Email, user.Permissions, user.Claims);
}
