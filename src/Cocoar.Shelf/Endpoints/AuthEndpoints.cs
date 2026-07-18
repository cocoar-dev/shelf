using Cocoar.Shelf.Identity;
using Cocoar.Shelf.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Endpoints;

/// <summary>
/// Auth endpoints on the cookie session. Login is the standard OIDC authorization-code flow
/// against modgud: <c>GET /login</c> challenges, modgud redirects back to the handler's callback,
/// the ticket event JIT-provisions the local user and mints the <c>shelf.auth</c> cookie.
/// A modgud browser session makes further logins (here and in other Cocoar apps) silent — SSO.
/// </summary>
public static partial class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth");

        auth.MapPost("/logout", Logout);
        auth.MapGet("/me", GetMe);

        return api;
    }

    /// <summary>Browser-facing login/logout pages (top-level, outside <c>/_api</c>).</summary>
    public static WebApplication MapAuthPages(this WebApplication app, bool modgudConfigured)
    {
        if (modgudConfigured)
        {
            // Real modgud login — OIDC challenge. After the callback the cookie is set.
            app.MapGet("/login", (string? returnUrl) =>
                Results.Challenge(
                    new AuthenticationProperties { RedirectUri = SafeReturn(returnUrl) },
                    [OpenIdConnectDefaults.AuthenticationScheme]));

            // Full sign-out: the app cookie AND the modgud session (front-channel end-session),
            // otherwise the next /login would silently sign right back in.
            app.MapGet("/logout", () =>
                Results.SignOut(
                    new AuthenticationProperties { RedirectUri = "/" },
                    [IdentityConstants.ApplicationScheme, OpenIdConnectDefaults.AuthenticationScheme]));
        }
        else
        {
            // No IdP configured (integration tests use the test seam) — cookie-only sign-out.
            app.MapGet("/logout", () =>
                Results.SignOut(
                    new AuthenticationProperties { RedirectUri = "/" },
                    [IdentityConstants.ApplicationScheme]));
        }

        return app;
    }

    /// <summary>Only same-origin relative paths survive — anything else falls back to "/".</summary>
    private static string SafeReturn(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : "/";

    private static async Task<IResult> Logout(SignInManager<UserDocument> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> GetMe(
        HttpContext httpContext,
        UserManager<UserDocument> userManager,
        ShelfOptions options)
    {
        if (CurrentUser.Id(httpContext) is not { } uid)
            return Results.Json(new { authenticated = false }, statusCode: 401);

        var user = await userManager.FindByIdAsync(uid.ToString());
        if (user is null || !user.IsActive)
            return Results.Json(new { authenticated = false }, statusCode: 401);

        return Results.Ok(new
        {
            authenticated = true,
            id = user.Id,
            email = user.Email,
            displayName = user.DisplayName ?? user.UserName,
            isAdmin = AdminCheck.IsAdmin(httpContext.User, options),
            permissions = AdminCheck.Permissions(httpContext.User),
        });
    }
}
