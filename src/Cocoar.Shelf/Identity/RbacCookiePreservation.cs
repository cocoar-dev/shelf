using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Identity;

/// <summary>
/// Cookie <c>OnValidatePrincipal</c> hook that keeps the modgud RBAC snapshot correct across the
/// <see cref="SecurityStampValidator"/>'s periodic principal regeneration: the validator rebuilds
/// the principal from the user store, which knows nothing about the <c>resource_access</c> claim
/// stamped at broker-login — without this hook every regeneration would silently drop admin rights.
/// Snapshot before validation, re-stamp after.
/// </summary>
internal static class RbacCookiePreservation
{
    public static async Task ValidatePreservingRbacAsync(CookieValidatePrincipalContext context)
    {
        var resourceAccess = context.Principal?.FindFirst(ModgudClaimsTransformation.ResourceAccessClaimType)?.Value;

        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal is null) return; // validator rejected + signed out (stamp rotated)

        if (context.Principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated) return;

        if (resourceAccess is not null
            && identity.FindFirst(ModgudClaimsTransformation.ResourceAccessClaimType) is null)
            identity.AddClaim(new Claim(ModgudClaimsTransformation.ResourceAccessClaimType, resourceAccess));
    }
}
