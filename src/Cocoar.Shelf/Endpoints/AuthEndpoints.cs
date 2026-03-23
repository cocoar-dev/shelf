using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Cocoar.Shelf.Endpoints;

public static partial class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth");

        auth.MapPost("/login", (Delegate)Login);
        auth.MapPost("/logout", (Delegate)Logout);
        auth.MapGet("/me", (Delegate)GetMe);

        return api;
    }

    private static async Task<IResult> Login(
        HttpContext httpContext,
        LoginRequest request,
        ShelfOptions options,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Auth");

        if (string.IsNullOrEmpty(options.ApiKey))
        {
            LogLoginDisabled(logger);
            return Results.Json(new { error = "Login is disabled (no API key configured)" }, statusCode: 503);
        }

        if (string.IsNullOrEmpty(request.ApiKey) || request.ApiKey != options.ApiKey)
        {
            LogLoginFailed(logger);
            return Results.Json(new { error = "Invalid API key" }, statusCode: 401);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "admin"),
            new(ClaimTypes.Role, "admin"),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        LogLoginSuccess(logger);
        return Results.Ok(new { name = "admin", role = "admin" });
    }

    private static async Task<IResult> Logout(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Ok(new { ok = true });
    }

    private static IResult GetMe(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
            return Results.Json(new { authenticated = false }, statusCode: 401);

        return Results.Ok(new
        {
            authenticated = true,
            name = httpContext.User.Identity.Name,
            role = httpContext.User.FindFirstValue(ClaimTypes.Role),
        });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login disabled: no API key configured")]
    private static partial void LogLoginDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login failed: invalid API key")]
    private static partial void LogLoginFailed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login successful")]
    private static partial void LogLoginSuccess(ILogger logger);
}

public record LoginRequest(string ApiKey);
