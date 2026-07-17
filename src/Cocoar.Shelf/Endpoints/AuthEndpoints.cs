using System.Security.Claims;
using Cocoar.Shelf.Identity;
using Cocoar.Shelf.Models;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Endpoints;

public static partial class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth");

        auth.MapPost("/login", Login);
        auth.MapPost("/logout", Logout);
        auth.MapGet("/me", GetMe);
        auth.MapPost("/change-password", ChangePassword);
        auth.MapPost("/forgot-password", ForgotPassword);
        auth.MapPost("/reset-password", ResetPassword);

        return api;
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        UserManager<UserDocument> userManager,
        AppSignInManager signInManager,
        IQuerySession session,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Auth");

        var user = await userManager.FindByNameAsync(request.UserName)
                   ?? await userManager.FindByEmailAsync(request.UserName);

        if (user == null || !user.IsActive)
        {
            LogLoginFailed(logger, request.UserName);
            // Anti-timing delay
            await Task.Delay(Random.Shared.Next(100, 300), ct);
            return Results.Json(new { error = "Invalid credentials" }, statusCode: 401);
        }

        var result = await signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            LogLoginSuccess(logger, user.UserName);
            return Results.Ok(new { name = user.DisplayName ?? user.UserName, userName = user.UserName });
        }

        if (result.RequiresTwoFactor)
        {
            var methods = await TwoFactorHelper.GetMethodsAsync(user, session, ct);
            return Results.Ok(new { requiresMfa = true, mfaMethods = methods });
        }

        if (result.IsLockedOut)
        {
            LogLoginLockedOut(logger, user.UserName);
            return Results.Json(new { error = "Account is locked. Please try again later." }, statusCode: 401);
        }

        LogLoginFailed(logger, request.UserName);
        return Results.Json(new { error = "Invalid credentials" }, statusCode: 401);
    }

    private static async Task<IResult> Logout(
        HttpContext httpContext,
        AppSignInManager signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> GetMe(
        HttpContext httpContext,
        UserManager<UserDocument> userManager,
        IQuerySession session,
        CancellationToken ct)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
            return Results.Json(new { authenticated = false }, statusCode: 401);

        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
            return Results.Json(new { authenticated = false }, statusCode: 401);

        var methods = await TwoFactorHelper.GetMethodsAsync(user, session, ct);

        return Results.Ok(new
        {
            authenticated = true,
            id = user.Id,
            userName = user.UserName,
            displayName = user.DisplayName,
            email = user.Email,
            has2FA = methods.Count > 0,
            twoFactorMethods = methods
        });
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        HttpContext httpContext,
        UserManager<UserDocument> userManager,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Auth");
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Results.Json(new { error = errors }, statusCode: 400);
        }

        LogPasswordChanged(logger, user.UserName);
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request,
        UserManager<UserDocument> userManager,
        Services.IEmailService emailService,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Auth");
        var user = await userManager.FindByNameAsync(request.UserNameOrEmail)
                   ?? await userManager.FindByEmailAsync(request.UserNameOrEmail);

        if (user?.Email != null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = Uri.EscapeDataString(token);
            var pathBase = httpContext.Request.PathBase.Value?.TrimEnd('/') ?? "";
            var link = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{pathBase}/reset-password?userId={user.Id}&token={encodedToken}";

            await emailService.SendAsync(
                user.Email,
                "Shelf Password Reset",
                $"<p>Click the link below to reset your password:</p><p><a href=\"{link}\">Reset Password</a></p><p>This link expires in 24 hours.</p>",
                ct);
        }

        // Always return success to prevent user enumeration
        return Results.Ok(new { message = "If the account exists, a reset link has been sent." });
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordRequest request,
        UserManager<UserDocument> userManager,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Auth");
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
            return Results.Json(new { error = "Invalid reset link" }, statusCode: 400);

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Results.Json(new { error = errors }, statusCode: 400);
        }

        LogPasswordReset(logger, user.UserName);
        return Results.Ok(new { ok = true });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Login successful: {UserName}")]
    private static partial void LogLoginSuccess(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login failed: {UserName}")]
    private static partial void LogLoginFailed(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login locked out: {UserName}")]
    private static partial void LogLoginLockedOut(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password changed: {UserName}")]
    private static partial void LogPasswordChanged(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset: {UserName}")]
    private static partial void LogPasswordReset(ILogger logger, string userName);
}
