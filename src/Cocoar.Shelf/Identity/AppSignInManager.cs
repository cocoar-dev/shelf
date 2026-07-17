using Cocoar.Shelf.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Cocoar.Shelf.Identity;

public class AppSignInManager : SignInManager<UserDocument>
{
    public AppSignInManager(
        UserManager<UserDocument> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<UserDocument> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<UserDocument>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<UserDocument> confirmation)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
    }

    public override Task<bool> IsTwoFactorEnabledAsync(UserDocument user)
    {
        // TOTP enabled via Identity
        if (user.TwoFactorEnabled)
            return Task.FromResult(true);

        // Email OTP enabled and user has email
        if (user.EmailOtpEnabled && !string.IsNullOrEmpty(user.Email))
            return Task.FromResult(true);

        return Task.FromResult(false);
    }
}
