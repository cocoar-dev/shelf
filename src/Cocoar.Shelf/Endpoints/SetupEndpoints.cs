using Cocoar.Shelf.Identity;
using Cocoar.Shelf.Models;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Endpoints;

public static partial class SetupEndpoints
{
    public static RouteGroupBuilder MapSetupEndpoints(this RouteGroupBuilder api)
    {
        var setup = api.MapGroup("/setup");

        setup.MapGet("/status", GetStatus);
        setup.MapPost("/create-admin", CreateAdmin);

        return api;
    }

    private static async Task<IResult> GetStatus(IQuerySession session, CancellationToken ct)
    {
        var hasUsers = await session.Query<UserDocument>().AnyAsync(ct);
        return Results.Ok(new { needsSetup = !hasUsers });
    }

    private static async Task<IResult> CreateAdmin(
        CreateAdminRequest request,
        UserManager<UserDocument> userManager,
        AppSignInManager signInManager,
        IQuerySession session,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Setup");

        // Guard: only works when no users exist
        var hasUsers = await session.Query<UserDocument>().AnyAsync(ct);
        if (hasUsers)
            return Results.Json(new { error = "Setup has already been completed" }, statusCode: 400);

        var user = new UserDocument
        {
            Id = Guid.NewGuid(),
            UserName = request.UserName,
            Email = request.Email,
            DisplayName = request.DisplayName,
            IsActive = true,
            LockoutEnabled = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            LogSetupFailed(logger, errors);
            return Results.Json(new { error = errors }, statusCode: 400);
        }

        // Auto-login
        await signInManager.SignInAsync(user, isPersistent: true);

        LogSetupCompleted(logger, user.UserName);
        return Results.Created("/_api/auth/me", new
        {
            id = user.Id,
            userName = user.UserName,
            displayName = user.DisplayName
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Setup failed: {Errors}")]
    private static partial void LogSetupFailed(ILogger logger, string errors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Initial admin created: {UserName}")]
    private static partial void LogSetupCompleted(ILogger logger, string userName);
}
