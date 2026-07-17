using System.Security.Cryptography;
using System.Text;
using Cocoar.Shelf.Models;
using Cocoar.Shelf.Services;
using Marten;

namespace Cocoar.Shelf.Identity;

public class MagicLinkService
{
    private const int TokenSizeBytes = 32;
    private const int ExpirationMinutes = 15;
    private const int RateLimitMinutes = 2;

    private readonly IDocumentStore _store;
    private readonly IEmailService _email;
    private readonly ILogger<MagicLinkService> _logger;

    public MagicLinkService(IDocumentStore store, IEmailService email, ILogger<MagicLinkService> logger)
    {
        _store = store;
        _email = email;
        _logger = logger;
    }

    public async Task<bool> SendLinkAsync(Guid userId, string emailAddress, string baseUrl, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();

        // Rate limiting
        var existing = await session.Query<MagicLinkChallenge>()
            .Where(c => c.UserId == userId && c.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(ExpirationMinutes - RateLimitMinutes))
            .AnyAsync(ct);

        if (existing)
            return false;

        var token = GenerateToken();
        var hash = HashToken(token);

        var challenge = new MagicLinkChallenge
        {
            Id = Guid.NewGuid(),
            TokenHash = hash,
            UserId = userId,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(ExpirationMinutes)
        };

        session.Store(challenge);
        await session.SaveChangesAsync(ct);

        var encodedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
        var link = $"{baseUrl}/magic-login?userId={userId}&token={Uri.EscapeDataString(encodedToken)}";

        await _email.SendAsync(
            emailAddress,
            "Shelf Login Link",
            $"<p>Click the link below to sign in:</p><p><a href=\"{link}\">Sign in to Shelf</a></p><p>This link expires in {ExpirationMinutes} minutes.</p>",
            ct);

        return true;
    }

    public async Task<Guid?> VerifyTokenAsync(Guid userId, string token, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var hash = HashToken(token);

        var challenges = await session.Query<MagicLinkChallenge>()
            .Where(c => c.UserId == userId && c.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(ct);

        foreach (var challenge in challenges)
        {
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(hash),
                    Encoding.UTF8.GetBytes(challenge.TokenHash)))
            {
                // Valid — one-time use, delete
                session.Delete(challenge);
                await session.SaveChangesAsync(ct);
                return userId;
            }
        }

        return null;
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenSizeBytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
