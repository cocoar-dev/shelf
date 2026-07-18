using Cocoar.Shelf.Models;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Identity;

/// <summary>
/// Marten-backed store for the thin local user layer. Identity is federated to modgud, so only the
/// base store plus email and security-stamp support remain — no passwords, lockout or 2FA.
/// </summary>
public class MartenUserStore :
    IUserStore<UserDocument>,
    IUserEmailStore<UserDocument>,
    IUserSecurityStampStore<UserDocument>
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

    // --- IUserEmailStore ---

    public Task SetEmailAsync(UserDocument user, string? email, CancellationToken ct)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(user.Email);

    public Task<bool> GetEmailConfirmedAsync(UserDocument user, CancellationToken ct)
        => Task.FromResult(true); // modgud owns verification; a brokered login means the identity is good

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

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
