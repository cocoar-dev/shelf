using Cocoar.Shelf.Models;
using Cocoar.Shelf.Services;
using Cocoar.Shelf.Services.Access;
using Marten;

namespace Cocoar.Shelf.Endpoints;

public record GroupUpsertRequest(
    string Name,
    string? Description,
    string? MembershipMode,
    IReadOnlyList<string>? MemberEmails,
    string? MembershipScript,
    bool? IsAdminGroup);

/// <summary>Dry-run request: evaluate a membership script against one user (by id or email).</summary>
public record GroupScriptTestRequest(string Script, Guid? UserId, string? Email);

/// <summary>
/// Admin CRUD over <see cref="Group"/> permission carriers, plus manual auto-membership recalc and a
/// script dry-run (Access Control v2). All endpoints require the <c>Admin</c> policy — groups grant
/// access, so only admins manage them.
/// </summary>
public static class GroupEndpoints
{
    public static RouteGroupBuilder MapGroupEndpoints(this RouteGroupBuilder api)
    {
        var groups = api.MapGroup("/groups").RequireAuthorization("Admin");

        groups.MapGet("", GetGroups);
        groups.MapGet("/{id:guid}", GetGroup);
        groups.MapPost("", CreateGroup);
        groups.MapPut("/{id:guid}", UpdateGroup);
        groups.MapDelete("/{id:guid}", DeleteGroup);
        groups.MapPost("/recalculate", RecalculateAll);
        groups.MapPost("/test-script", TestScript);

        return api;
    }

    private static async Task<IResult> GetGroups(IGroupService groupService)
    {
        var groups = await groupService.GetAllAsync();
        return Results.Ok(groups.Select(ToSummary));
    }

    private static async Task<IResult> GetGroup(Guid id, IGroupService groupService, IDocumentStore store)
    {
        var group = await groupService.GetAsync(id);
        if (group is null)
            return Results.NotFound();

        // Resolve materialized auto-members to user summaries for the read-only Members view.
        await using var session = store.QuerySession();
        var users = await session.LoadManyAsync<UserDocument>(group.AutoMemberUserIds.ToArray());
        var autoMembers = users.Select(u => new { u.Id, u.Email, DisplayName = u.DisplayName ?? u.UserName });

        return Results.Ok(new
        {
            group.Id,
            group.Name,
            group.Description,
            MembershipMode = group.MembershipMode.ToString(),
            group.MemberEmails,
            group.MembershipScript,
            group.IsAdminGroup,
            group.MembershipLastError,
            AutoMembers = autoMembers,
        });
    }

    private static async Task<IResult> CreateGroup(
        GroupUpsertRequest request, IGroupService groupService, IGroupMembershipRecalculator recalculator)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.Json(new { error = "Group name is required" }, statusCode: 400);
        if (!TryParseMode(request.MembershipMode, out var mode))
            return Results.Json(new { error = $"Invalid membership mode '{request.MembershipMode}'" }, statusCode: 400);
        if (mode == Models.MembershipMode.Auto && string.IsNullOrWhiteSpace(request.MembershipScript))
            return Results.Json(new { error = "Auto membership requires a script" }, statusCode: 400);

        var group = new Group { Id = Guid.NewGuid(), Name = request.Name.Trim() };
        Apply(group, request, mode);
        await groupService.CreateAsync(group);

        // Materialize auto-members immediately so the new group's grants take effect at once.
        if (mode == Models.MembershipMode.Auto)
            await recalculator.RecalculateGroupAsync(group.Id);

        return Results.Created($"/_api/groups/{group.Id}", ToSummary(group));
    }

    private static async Task<IResult> UpdateGroup(
        Guid id, GroupUpsertRequest request, IGroupService groupService, IGroupMembershipRecalculator recalculator)
    {
        var existing = await groupService.GetAsync(id);
        if (existing is null)
            return Results.NotFound();
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.Json(new { error = "Group name is required" }, statusCode: 400);
        if (!TryParseMode(request.MembershipMode, out var mode))
            return Results.Json(new { error = $"Invalid membership mode '{request.MembershipMode}'" }, statusCode: 400);
        if (mode == Models.MembershipMode.Auto && string.IsNullOrWhiteSpace(request.MembershipScript))
            return Results.Json(new { error = "Auto membership requires a script" }, statusCode: 400);

        Apply(existing, request, mode);
        await groupService.UpdateAsync(existing);

        // Recompute this group's materialized auto-members against the new script/mode.
        await recalculator.RecalculateGroupAsync(existing.Id);

        return Results.Ok(ToSummary(existing));
    }

    private static async Task<IResult> DeleteGroup(Guid id, IGroupService groupService)
        => await groupService.DeleteAsync(id) ? Results.NoContent() : Results.NotFound();

    private static async Task<IResult> RecalculateAll(IGroupMembershipRecalculator recalculator)
    {
        await recalculator.RecalculateAllAsync();
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> TestScript(
        GroupScriptTestRequest request, IGroupMembershipEvaluator evaluator, IDocumentStore store)
    {
        if (string.IsNullOrWhiteSpace(request.Script))
            return Results.Json(new { error = "Script is required" }, statusCode: 400);

        await using var session = store.QuerySession();
        UserDocument? user = request.UserId is { } id
            ? await session.LoadAsync<UserDocument>(id)
            : !string.IsNullOrWhiteSpace(request.Email)
                ? (await session.Query<UserDocument>()
                    .Where(u => u.Email != null && u.Email.ToLower() == request.Email.ToLower()).ToListAsync())
                    .FirstOrDefault()
                : null;

        if (user is null)
            return Results.Json(new { error = "No user found for the given id/email" }, statusCode: 404);

        var result = evaluator.Evaluate(request.Script, GroupMembershipContext.FromUser(user));
        return Results.Ok(new
        {
            matched = result.Matched,
            error = result.Error,
            user = new { user.Id, user.Email, DisplayName = user.DisplayName ?? user.UserName },
        });
    }

    private static object ToSummary(Group g) => new
    {
        g.Id,
        g.Name,
        g.Description,
        MembershipMode = g.MembershipMode.ToString(),
        g.MemberEmails,
        g.MembershipScript,
        g.IsAdminGroup,
        AutoMemberCount = g.AutoMemberUserIds.Count,
        g.MembershipLastError,
    };

    private static void Apply(Group group, GroupUpsertRequest request, MembershipMode mode)
    {
        group.Name = request.Name.Trim();
        group.Description = request.Description;
        group.MembershipMode = mode;
        group.MemberEmails = NormalizeEmails(request.MemberEmails);
        group.MembershipScript = mode == Models.MembershipMode.Auto ? request.MembershipScript : null;
        group.IsAdminGroup = request.IsAdminGroup ?? false;
    }

    private static IReadOnlyList<string> NormalizeEmails(IReadOnlyList<string>? emails) =>
        emails?.Select(e => e.Trim()).Where(e => e.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList() ?? [];

    private static bool TryParseMode(string? mode, out MembershipMode parsed)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            parsed = Models.MembershipMode.Manual;
            return true;
        }
        return Enum.TryParse(mode, ignoreCase: true, out parsed);
    }
}
