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
}
