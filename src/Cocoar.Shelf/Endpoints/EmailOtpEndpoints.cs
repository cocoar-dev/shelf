using Cocoar.Shelf.Identity;
using Cocoar.Shelf.Models;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Endpoints;

public static class EmailOtpEndpoints
{
    public static RouteGroupBuilder MapEmailOtpEndpoints(this RouteGroupBuilder api)
    {
        var otp = api.MapGroup("/auth/email-otp");

        otp.MapGet("/status", GetStatus);
        otp.MapPost("/enable", Enable);
        otp.MapPost("/disable", Disable);
        otp.MapPost("/login/request", RequestCode);
        otp.MapPost("/login", VerifyCode);

        return otp;
    }

    private static async Task<IResult> GetStatus(
        HttpContext httpContext,
        UserManager<UserDocument> userManager)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        return Results.Ok(new
        {
            enabled = user.EmailOtpEnabled,
            hasEmail = !string.IsNullOrEmpty(user.Email)
        });
    }

    private static async Task<IResult> Enable(
        HttpContext httpContext,
        UserManager<UserDocument> userManager)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        if (string.IsNullOrEmpty(user.Email))
            return Results.Json(new { error = "Email address is required" }, statusCode: 400);

        user.EmailOtpEnabled = true;
        await userManager.UpdateAsync(user);
        return Results.Ok(new { enabled = true });
    }

    private static async Task<IResult> Disable(
        HttpContext httpContext,
        UserManager<UserDocument> userManager)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        user.EmailOtpEnabled = false;
        await userManager.UpdateAsync(user);
        return Results.Ok(new { enabled = false });
    }

    private static async Task<IResult> RequestCode(
        AppSignInManager signInManager,
        EmailOtpService otpService,
        CancellationToken ct)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user?.Email == null)
            return Results.Json(new { error = "No pending 2FA login" }, statusCode: 401);

        await otpService.SendCodeAsync(user.Id, user.Email, ct);
        return Results.Ok(new { sent = true });
    }

    private static async Task<IResult> VerifyCode(
        EmailOtpLoginRequest request,
        AppSignInManager signInManager,
        UserManager<UserDocument> userManager,
        EmailOtpService otpService,
        CancellationToken ct)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
            return Results.Json(new { error = "No pending 2FA login" }, statusCode: 401);

        var isValid = await otpService.VerifyCodeAsync(user.Id, request.Code, ct);
        if (!isValid)
            return Results.Json(new { error = "Invalid or expired code" }, statusCode: 401);

        await signInManager.SignInAsync(user, request.RememberMe);
        return Results.Ok(new { ok = true });
    }
}
