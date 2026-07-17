using Cocoar.Shelf.Models;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Identity;

public class MartenUserStore :
    IUserStore<UserDocument>,
    IUserPasswordStore<UserDocument>,
    IUserEmailStore<UserDocument>,
    IUserSecurityStampStore<UserDocument>,
    IUserLockoutStore<UserDocument>,
    IUserTwoFactorStore<UserDocument>,
    IUserAuthenticatorKeyStore<UserDocument>,
    IUserPhoneNumberStore<UserDocument>
{
    private readonly IDocumentStore _store;

    public MartenUserStore(IDocumentStore store)
    {
        _store = store;
    }

    // --- IUserStore ---

    public async Task<IdentityResult> CreateAsync(UserDocument user, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        session.Store(user);
        await session.SaveChangesAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(UserDocument user, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        session.Update(user);
        await session.SaveChangesAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(UserDocument user, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        session.Delete(user);
        await session.SaveChangesAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<UserDocument?> FindByIdAsync(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var id)) return null;
        await using var session = _store.QuerySession();
        return await session.LoadAsync<UserDocument>(id, ct);
    }

    public async Task<UserDocument?> FindByNameAsync(string normalizedUserName, CancellationToken ct)
    {
        await using var session = _store.QuerySession();
        return await session.Query<UserDocument>()
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, ct);
    }

    public Task<string> GetUserIdAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(user.Id.ToString());

    public Task<string?> GetUserNameAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult<string?>(user.UserName);

    public Task SetUserNameAsync(UserDocument user, string? userName, CancellationToken ct)
    {
        user.UserName = userName ?? "";
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(UserDocument user, string? normalizedName, CancellationToken ct)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    // --- IUserPasswordStore ---

    public Task SetPasswordHashAsync(UserDocument user, string? passwordHash, CancellationToken ct)
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

    // --- IUserEmailStore ---

    public Task SetEmailAsync(UserDocument user, string? email, CancellationToken ct)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(user.Email);

    public Task<bool> GetEmailConfirmedAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(true); // Shelf doesn't require email confirmation

    public Task SetEmailConfirmedAsync(UserDocument user, bool confirmed, CancellationToken ct)
        => Task.CompletedTask;

    public async Task<UserDocument?> FindByEmailAsync(string normalizedEmail, CancellationToken ct)
    {
        await using var session = _store.QuerySession();
        return await session.Query<UserDocument>()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
    }

    public Task<string?> GetNormalizedEmailAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(UserDocument user, string? normalizedEmail, CancellationToken ct)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    // --- IUserSecurityStampStore ---

    public Task SetSecurityStampAsync(UserDocument user, string stamp, CancellationToken ct)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(user.SecurityStamp);

    // --- IUserLockoutStore ---

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(UserDocument user, DateTimeOffset? lockoutEnd, CancellationToken ct)
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(++user.AccessFailedCount);

    public Task ResetAccessFailedCountAsync(UserDocument user, CancellationToken ct)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(user.AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(user.LockoutEnabled);

    public Task SetLockoutEnabledAsync(UserDocument user, bool enabled, CancellationToken ct)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    // --- IUserTwoFactorStore ---

    public Task SetTwoFactorEnabledAsync(UserDocument user, bool enabled, CancellationToken ct)
    {
        user.TwoFactorEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task<bool> GetTwoFactorEnabledAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(user.TwoFactorEnabled);

    // --- IUserAuthenticatorKeyStore ---

    public Task SetAuthenticatorKeyAsync(UserDocument user, string key, CancellationToken ct)
    {
        user.AuthenticatorKey = key;
        return Task.CompletedTask;
    }

    public Task<string?> GetAuthenticatorKeyAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(user.AuthenticatorKey);

    // --- IUserPhoneNumberStore (no-op stubs, required by Identity) ---

    public Task SetPhoneNumberAsync(UserDocument user, string? phoneNumber, CancellationToken ct)
        => Task.CompletedTask;

    public Task<string?> GetPhoneNumberAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult<string?>(null);

    public Task<bool> GetPhoneNumberConfirmedAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(false);

    public Task SetPhoneNumberConfirmedAsync(UserDocument user, bool confirmed, CancellationToken ct)
        => Task.CompletedTask;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
