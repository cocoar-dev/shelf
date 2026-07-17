using Cocoar.Shelf.Models;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Endpoints;

public record SetActiveRequest(bool IsActive);

/// <summary>
/// Read-only-ish admin surface over the thin local user layer. Users are created via JIT
/// provisioning at modgud login — there is no local create/password management anymore. What
/// remains: list the mirrors, deactivate one (blocks login), delete one (it re-provisions on the
/// user's next modgud login).
/// </summary>
public static partial class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/users").RequireAuthorization("Admin");

        users.MapGet("/", ListUsers);
        users.MapPut("/{id:guid}/active", SetActive);
        users.MapDelete("/{id:guid}", DeleteUser);

        return api;
    }

    private static async Task<IResult> ListUsers(
        IQuerySession session,
        CancellationToken ct)
    {
        var users = await session.Query<UserDocument>()
            .OrderBy(u => u.UserName)
            .ToListAsync(ct);

        return Results.Ok(users.Select(u => new
        {
            u.Id,
            u.UserName,
            u.DisplayName,
            u.Email,
            u.IsActive,
            u.CreatedAt
        }));
    }

    private static async Task<IResult> SetActive(
        Guid id,
        SetActiveRequest request,
        UserManager<UserDocument> userManager)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null)
            return Results.Json(new { error = "User not found" }, statusCode: 404);

        user.IsActive = request.IsActive;
        await userManager.UpdateAsync(user);
        // Rotate the stamp so existing cookie sessions of a deactivated user die at the next
        // security-stamp validation pass, not only at re-login.
        if (!request.IsActive)
            await userManager.UpdateSecurityStampAsync(user);
        return Results.Ok(new { user.Id, user.IsActive });
    }

    private static async Task<IResult> DeleteUser(
        Guid id,
        UserManager<UserDocument> userManager,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Users");
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null)
            return Results.Json(new { error = "User not found" }, statusCode: 404);

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Results.Json(new { error = errors }, statusCode: 400);
        }

        LogUserDeleted(logger, user.UserName);
        return Results.NoContent();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User deleted: {UserName}")]
    private static partial void LogUserDeleted(ILogger logger, string userName);
}
