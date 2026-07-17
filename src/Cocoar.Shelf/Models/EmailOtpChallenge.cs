namespace Cocoar.Shelf.Models;

public class EmailOtpChallenge
{
    public Guid Id { get; set; } // = UserId (one challenge per user)

    public string CodeHash { get; set; } = "";

    public DateTimeOffset ExpiresAt { get; set; }

    public int Attempts { get; set; }
}
