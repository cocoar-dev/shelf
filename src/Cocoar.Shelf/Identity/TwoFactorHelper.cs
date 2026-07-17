using Cocoar.Shelf.Models;
using Marten;

namespace Cocoar.Shelf.Identity;

public static class TwoFactorHelper
{
    public static async Task<List<string>> GetMethodsAsync(UserDocument user, IQuerySession session, CancellationToken ct = default)
    {
        var methods = new List<string>();

        if (user.TwoFactorEnabled)
            methods.Add("totp");

        if (user.EmailOtpEnabled && !string.IsNullOrEmpty(user.Email))
            methods.Add("email");

        var passkeyCount = await session.Query<StoredPasskeyCredential>()
            .Where(c => c.UserId == user.Id)
            .CountAsync(ct);

        if (passkeyCount > 0)
            methods.Add("passkey");

        return methods;
    }
}
