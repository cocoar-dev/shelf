using Cocoar.Shelf.Identity;
using Cocoar.Shelf.Models;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Endpoints;

public static class MagicLinkEndpoints
{
    public static RouteGroupBuilder MapMagicLinkEndpoints(this RouteGroupBuilder api)
    {
        var magic = api.MapGroup("/auth/magic-link");

        magic.MapPost("/request", RequestLink);
        magic.MapPost("/login", LoginWithLink);

        return magic;
    }

    private static async Task<IResult> RequestLink(
        MagicLinkRequest request,
        UserManager<UserDocument> userManager,
        MagicLinkService magicLinkService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user?.Email != null && user.IsActive)
        {
            var pathBase = httpContext.Request.PathBase.Value?.TrimEnd('/') ?? "";
            var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{pathBase}";
            await magicLinkService.SendLinkAsync(user.Id, user.Email, baseUrl, ct);
        }

        // Anti-timing delay
        await Task.Delay(Random.Shared.Next(100, 300), ct);

        // Always return success to prevent user enumeration
        return Results.Ok(new { message = "If the email is registered, a login link has been sent." });
    }

    private static async Task<IResult> LoginWithLink(
        MagicLinkLoginRequest request,
        UserManager<UserDocument> userManager,
        AppSignInManager signInManager,
        MagicLinkService magicLinkService,
        CancellationToken ct)
    {
        // Decode token
        string token;
        try
        {
            token = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(request.Token));
        }
        catch
        {
            return Results.Json(new { error = "Invalid link" }, statusCode: 400);
        }

        var userId = await magicLinkService.VerifyTokenAsync(request.UserId, token, ct);
        if (userId == null)
        {
            await Task.Delay(Random.Shared.Next(100, 300), ct);
            return Results.Json(new { error = "Invalid or expired link" }, statusCode: 401);
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null || !user.IsActive)
            return Results.Json(new { error = "Account not found" }, statusCode: 401);

        // Magic link bypasses 2FA
        await signInManager.SignInAsync(user, request.RememberMe);
        return Results.Ok(new { ok = true, name = user.DisplayName ?? user.UserName });
    }
}
