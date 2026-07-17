namespace Cocoar.Shelf.Models;

public class UserDocument
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = "";

    public string? NormalizedUserName { get; set; }

    public string? Email { get; set; }

    public string? NormalizedEmail { get; set; }

    public string? DisplayName { get; set; }

    public string? PasswordHash { get; set; }

    public string? SecurityStamp { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public bool IsActive { get; set; } = true;

    public bool LockoutEnabled { get; set; } = true;

    public DateTimeOffset? LockoutEnd { get; set; }

    public int AccessFailedCount { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public string? AuthenticatorKey { get; set; }

    public bool EmailOtpEnabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
