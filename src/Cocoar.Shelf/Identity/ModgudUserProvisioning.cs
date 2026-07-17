using System.Security.Claims;
using Cocoar.Shelf.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Identity;

/// <summary>
/// Just-in-time provisioning of the thin local <see cref="UserDocument"/> mirror for a federated
/// modgud identity, keyed by the modgud <c>sub</c>. Identity (credentials, verification, 2FA) lives
/// in modgud; the local doc exists to hang app-specific user state off (preferences, activity) and
/// to carry the cookie session via ASP.NET Identity's plumbing.
/// </summary>
public static class ModgudUserProvisioning
{
    /// <summary>Creates (or returns) the local user for a modgud <c>sub</c>. Race-safe: a concurrent
    /// first login for the same sub may have created it already — that's fine.</summary>
    public static async Task<UserDocument> EnsureLocalUserAsync(
        UserManager<UserDocument> users, Guid sub, string? email, string? name)
    {
        if (await users.FindByIdAsync(sub.ToString()) is { } existing) return existing;

        // Fall back to a synthetic, unique address if userinfo gave us none — the username validator
        // still needs a value, and a later login can correct it.
        email ??= $"{sub:N}@modgud.local";

        var user = new UserDocument
        {
            Id = sub,
            UserName = email,
            Email = email,
            DisplayName = name,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var result = await users.CreateAsync(user);
        if (!result.Succeeded)
        {
            if (await users.FindByIdAsync(sub.ToString()) is { } raced) return raced;
            throw new InvalidOperationException("create user failed: " +
                string.Join(",", result.Errors.Select(e => e.Description)));
        }

        return user;
    }

    /// <summary>
    /// Signs the user in via the application cookie, stamping the raw modgud <c>resource_access</c>
    /// JSON (when present) onto the cookie identity as a string claim so the per-request
    /// <see cref="ModgudClaimsTransformation"/> flattens RBAC (admin) claims. Falls back to the plain
    /// <c>SignInManager.SignInAsync</c> when there's nothing to add.
    /// </summary>
    public static async Task SignInWithRbacAsync(HttpContext ctx, SignInManager<UserDocument> signIn,
        UserDocument user, string? resourceAccess)
    {
        if (string.IsNullOrWhiteSpace(resourceAccess))
        {
            await signIn.SignInAsync(user, isPersistent: true);
            return;
        }
        var principal = await signIn.CreateUserPrincipalAsync(user);
        if (principal.Identity is ClaimsIdentity identity
            && identity.FindFirst(ModgudClaimsTransformation.ResourceAccessClaimType) is null)
        {
            identity.AddClaim(new Claim(ModgudClaimsTransformation.ResourceAccessClaimType, resourceAccess));
        }
        await ctx.SignInAsync(IdentityConstants.ApplicationScheme, principal,
            new AuthenticationProperties { IsPersistent = true });
    }
}

/// <summary>The authenticated user id from the cookie principal (ASP.NET Identity → NameIdentifier).</summary>
public static class CurrentUser
{
    public static Guid? Id(ClaimsPrincipal principal)
    {
        var v = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value;
        return Guid.TryParse(v, out var id) ? id : null;
    }

    public static Guid? Id(HttpContext ctx) => Id(ctx.User);
}
