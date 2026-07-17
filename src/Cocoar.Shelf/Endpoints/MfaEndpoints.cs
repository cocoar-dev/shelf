using System.Text;
using Cocoar.Shelf.Identity;
using Cocoar.Shelf.Models;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Endpoints;

public static class MfaEndpoints
{
    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    public static RouteGroupBuilder MapMfaEndpoints(this RouteGroupBuilder api)
    {
        var mfa = api.MapGroup("/auth/mfa");

        mfa.MapGet("/status", GetStatus);
        mfa.MapPost("/setup", Setup);
        mfa.MapPost("/verify", Verify);
        mfa.MapPost("/disable", Disable);
        mfa.MapPost("/login", MfaLogin);

        return mfa;
    }

    private static async Task<IResult> GetStatus(
        HttpContext httpContext,
        UserManager<UserDocument> userManager)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        return Results.Ok(new { enabled = user.TwoFactorEnabled });
    }

    private static async Task<IResult> Setup(
        HttpContext httpContext,
        UserManager<UserDocument> userManager)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        await userManager.ResetAuthenticatorKeyAsync(user);
        var unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);

        if (string.IsNullOrEmpty(unformattedKey))
            return Results.Json(new { error = "Failed to generate authenticator key" }, statusCode: 500);

        var authenticatorUri = string.Format(
            AuthenticatorUriFormat,
            Uri.EscapeDataString("Shelf"),
            Uri.EscapeDataString(user.UserName),
            unformattedKey);

        return Results.Ok(new
        {
            sharedKey = FormatKey(unformattedKey),
            authenticatorUri
        });
    }

    private static async Task<IResult> Verify(
        MfaLoginRequest request,
        HttpContext httpContext,
        UserManager<UserDocument> userManager)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        var isValid = await userManager.VerifyTwoFactorTokenAsync(
            user,
            userManager.Options.Tokens.AuthenticatorTokenProvider,
            request.Code);

        if (!isValid)
            return Results.Json(new { error = "Invalid verification code" }, statusCode: 400);

        await userManager.SetTwoFactorEnabledAsync(user, true);
        return Results.Ok(new { enabled = true });
    }

    private static async Task<IResult> Disable(
        HttpContext httpContext,
        UserManager<UserDocument> userManager)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        return Results.Ok(new { enabled = false });
    }

    private static async Task<IResult> MfaLogin(
        MfaLoginRequest request,
        AppSignInManager signInManager)
    {
        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(
            request.Code, request.RememberMe, request.RememberMachine);

        if (result.Succeeded)
            return Results.Ok(new { ok = true });

        if (result.IsLockedOut)
            return Results.Json(new { error = "Account is locked" }, statusCode: 401);

        return Results.Json(new { error = "Invalid code" }, statusCode: 401);
    }

    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
            result.Append(unformattedKey.AsSpan(currentPosition));
        return result.ToString().TrimEnd();
    }
}
