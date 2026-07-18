using Cocoar.Shelf.Identity;
using Cocoar.Shelf.Models;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Endpoints;

public record OtpRequestRequest(string Email);
public record OtpVerifyRequest(string Email, string Code);

/// <summary>
/// Auth endpoints on the broker-minted cookie session. Login is federated to modgud
/// (<see cref="ModgudLoginBroker"/>): the SPA posts email + code here, the backend brokers modgud's
/// native OTP grant server-to-server and mints the <c>shelf.auth</c> cookie. No local credentials.
/// </summary>
public static partial class AuthEndpoints
{
    private const string GenericOtpMessage = "If the email is registered, a login code has been sent.";
    private const string InvalidCodeMessage = "Invalid or expired code";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth");

        auth.MapPost("/otp/request", RequestOtp);
        auth.MapPost("/otp/verify", VerifyOtp);
        auth.MapPost("/logout", Logout);
        auth.MapGet("/me", GetMe);

        return api;
    }

    private static async Task<IResult> RequestOtp(
        OtpRequestRequest request,
        ModgudLoginBroker broker,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            try { await broker.RequestOtpAsync(request.Email.Trim(), ct); }
            catch { /* swallow — the response stays uniform regardless (anti-enumeration) */ }
        }
        return Results.Ok(new { message = GenericOtpMessage });
    }

    private static async Task<IResult> VerifyOtp(
        OtpVerifyRequest request,
        HttpContext httpContext,
        ModgudLoginBroker broker,
        UserManager<UserDocument> userManager,
        SignInManager<UserDocument> signInManager,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Auth");

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code))
            return Results.Json(new { error = InvalidCodeMessage }, statusCode: 400);

        var identity = await broker.VerifyOtpAsync(request.Email.Trim(), request.Code.Trim(), ct);
        if (identity is null)
        {
            LogLoginFailed(logger, request.Email);
            return Results.Json(new { error = InvalidCodeMessage }, statusCode: 401);
        }

        // JIT the thin local user keyed to the modgud sub.
        var user = await ModgudUserProvisioning.EnsureLocalUserAsync(
            userManager, identity.Sub, identity.Email, identity.Name);

        if (!user.IsActive)
        {
            LogLoginDeactivated(logger, user.UserName);
            return Results.Json(new { error = "Account is deactivated" }, statusCode: 403);
        }

        await ModgudUserProvisioning.SignInWithRbacAsync(httpContext, signInManager, user, identity.ResourceAccess);
        LogLoginSuccess(logger, user.UserName);
        return Results.Ok(new { id = user.Id, email = user.Email, displayName = user.DisplayName ?? user.UserName });
    }

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Login successful: {UserName}")]
    private static partial void LogLoginSuccess(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login failed: {UserName}")]
    private static partial void LogLoginFailed(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login rejected, account deactivated: {UserName}")]
    private static partial void LogLoginDeactivated(ILogger logger, string userName);
}
