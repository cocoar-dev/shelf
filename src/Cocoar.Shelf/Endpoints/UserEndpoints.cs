using Cocoar.Shelf.Models;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Endpoints;

public static partial class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/users");

        users.MapGet("/", ListUsers)
            .AddEndpointFilter<ApiKeyFilter>();
        users.MapPost("/", CreateUser)
            .AddEndpointFilter<ApiKeyFilter>();
        users.MapPut("/{id:guid}", UpdateUser)
            .AddEndpointFilter<ApiKeyFilter>();
        users.MapPut("/{id:guid}/password", SetPassword)
            .AddEndpointFilter<ApiKeyFilter>();
        users.MapPut("/{id:guid}/active", SetActive)
            .AddEndpointFilter<ApiKeyFilter>();
        users.MapDelete("/{id:guid}", DeleteUser)
            .AddEndpointFilter<ApiKeyFilter>();

        return users;
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
            hasPassword = !string.IsNullOrEmpty(u.PasswordHash),
            u.TwoFactorEnabled,
            u.EmailOtpEnabled,
            u.CreatedAt
        }));
    }

    private static async Task<IResult> CreateUser(
        CreateUserRequest request,
        UserManager<UserDocument> userManager,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Cocoar.Shelf.Users");

        var user = new UserDocument
        {
            Id = Guid.NewGuid(),
            UserName = request.UserName,
            Email = request.Email,
            DisplayName = request.DisplayName,
            IsActive = true,
            LockoutEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        IdentityResult result;
        if (!string.IsNullOrEmpty(request.Password))
        {
            result = await userManager.CreateAsync(user, request.Password);
        }
        else
        {
            result = await userManager.CreateAsync(user);
        }

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Results.Json(new { error = errors }, statusCode: 400);
        }

        LogUserCreated(logger, user.UserName);
        return Results.Created($"/_api/users/{user.Id}", new
        {
            user.Id,
            user.UserName,
            user.DisplayName,
            user.Email,
            user.IsActive
        });
    }

    private static async Task<IResult> UpdateUser(
        Guid id,
        UpdateUserRequest request,
        UserManager<UserDocument> userManager)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null)
            return Results.Json(new { error = "User not found" }, statusCode: 404);

        if (request.DisplayName != null)
            user.DisplayName = request.DisplayName;

        if (request.Email != null)
        {
            user.Email = request.Email;
            user.NormalizedEmail = request.Email.ToUpperInvariant();
        }

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Results.Json(new { error = errors }, statusCode: 400);
        }

        return Results.Ok(new
        {
            user.Id,
            user.UserName,
            user.DisplayName,
            user.Email,
            user.IsActive
        });
    }

    private static async Task<IResult> SetPassword(
        Guid id,
        SetPasswordRequest request,
        UserManager<UserDocument> userManager)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null)
            return Results.Json(new { error = "User not found" }, statusCode: 404);

        // Remove existing password if any, then add new
        if (await userManager.HasPasswordAsync(user))
            await userManager.RemovePasswordAsync(user);

        var result = await userManager.AddPasswordAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Results.Json(new { error = errors }, statusCode: 400);
        }

        return Results.Ok(new { ok = true });
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

    [LoggerMessage(Level = LogLevel.Information, Message = "User created: {UserName}")]
    private static partial void LogUserCreated(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Information, Message = "User deleted: {UserName}")]
    private static partial void LogUserDeleted(ILogger logger, string userName);
}
