using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Cocoar.Shelf.Models;
using Cocoar.Shelf.Services;
using Marten;

namespace Cocoar.Shelf.Identity;

public class EmailOtpService
{
    private const int CodeLength = 6;
    private const int ExpirationMinutes = 10;
    private const int RateLimitMinutes = 2;
    private const int MaxAttempts = 3;

    private readonly IDocumentStore _store;
    private readonly IEmailService _email;
    private readonly ILogger<EmailOtpService> _logger;

    public EmailOtpService(IDocumentStore store, IEmailService email, ILogger<EmailOtpService> logger)
    {
        _store = store;
        _email = email;
        _logger = logger;
    }

    public async Task<bool> SendCodeAsync(Guid userId, string emailAddress, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();

        // Rate limiting: check existing challenge
        var existing = await session.LoadAsync<EmailOtpChallenge>(userId, ct);
        if (existing != null && existing.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(ExpirationMinutes - RateLimitMinutes))
            return false; // Too soon

        var code = GenerateCode();
        var hash = HashCode(code);

        var challenge = new EmailOtpChallenge
        {
            Id = userId,
            CodeHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(ExpirationMinutes),
            Attempts = 0
        };

        session.Store(challenge);
        await session.SaveChangesAsync(ct);

        await _email.SendAsync(
            emailAddress,
            "Shelf Login Code",
            $"<p>Your login code is: <strong>{code}</strong></p><p>This code expires in {ExpirationMinutes} minutes.</p>",
            ct);

        return true;
    }

    public async Task<bool> VerifyCodeAsync(Guid userId, string code, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var challenge = await session.LoadAsync<EmailOtpChallenge>(userId, ct);

        if (challenge == null)
            return false;

        if (challenge.ExpiresAt < DateTimeOffset.UtcNow)
        {
            session.Delete(challenge);
            await session.SaveChangesAsync(ct);
            return false;
        }

        if (challenge.Attempts >= MaxAttempts)
        {
            session.Delete(challenge);
            await session.SaveChangesAsync(ct);
            return false;
        }

        var hash = HashCode(code);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(hash),
                Encoding.UTF8.GetBytes(challenge.CodeHash)))
        {
            challenge.Attempts++;
            session.Update(challenge);
            await session.SaveChangesAsync(ct);
            return false;
        }

        // Valid — remove challenge
        session.Delete(challenge);
        await session.SaveChangesAsync(ct);
        return true;
    }

    private static string GenerateCode()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }
}
