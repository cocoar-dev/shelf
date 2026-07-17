namespace Cocoar.Shelf.Models;

public class MagicLinkChallenge
{
    public Guid Id { get; set; }

    public string TokenHash { get; set; } = "";

    public Guid UserId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
