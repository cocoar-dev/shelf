namespace Cocoar.Shelf.Models;

/// <summary>The kind of principal a grant refers to (Access Control v2).</summary>
public enum PrincipalKind
{
    Group = 0,
    User = 1,
}

/// <summary>
/// Something a product's read access can be granted to. Both <see cref="Group"/> and
/// <see cref="UserDocument"/> are principals, so either can be assigned to a product.
/// </summary>
public interface IPrincipal
{
    PrincipalKind PrincipalKind { get; }

    /// <summary>Stable reference id: a group's <c>Id</c> (GUID string) or a user's email. Email keys
    /// users so a grant can be staged before the user's first (JIT-provisioning) login.</summary>
    string PrincipalId { get; }

    string PrincipalDisplayName { get; }
}

/// <summary>
/// A reference to a principal, stored on a product's read-access list. Kind-tagged so groups (by id)
/// and users (by email) share one list — the product-side, principal-oriented grant.
/// </summary>
public sealed record PrincipalRef(PrincipalKind Kind, string Id)
{
    public static PrincipalRef ForGroup(Guid id) => new(PrincipalKind.Group, id.ToString());
    public static PrincipalRef ForUser(string email) => new(PrincipalKind.User, email);
}
