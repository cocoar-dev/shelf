using Cocoar.Shelf.Models;
using Marten;

namespace Cocoar.Shelf.Services.Access;

public sealed class GroupMembershipRecalculator(
    IDocumentStore store, IGroupMembershipEvaluator evaluator) : IGroupMembershipRecalculator
{
    public async Task RecalculateForUserAsync(Guid userId, CancellationToken ct = default)
    {
        await using var session = store.LightweightSession();
        var user = await session.LoadAsync<UserDocument>(userId, ct);
        if (user is null)
            return;

        var context = GroupMembershipContext.FromUser(user);
        var autoGroups = await LoadAutoGroupsAsync(session, ct);

        var changed = false;
        foreach (var group in autoGroups)
        {
            var ids = group.AutoMemberUserIds.ToHashSet();
            var matched = group.MembershipScript is { } script && evaluator.IsMember(script, context);
            var mutated = matched ? ids.Add(userId) : ids.Remove(userId);
            if (!mutated)
                continue;

            group.AutoMemberUserIds = [.. ids];
            session.Store(group);
            changed = true;
        }

        if (changed)
            await session.SaveChangesAsync(ct);
    }

    public async Task RecalculateGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        await using var session = store.LightweightSession();
        var group = await session.LoadAsync<Group>(groupId, ct);
        if (group is null || group.IsDeleted)
            return;

        var users = await session.Query<UserDocument>().ToListAsync(ct);
        RecomputeGroup(session, group, users);
        await session.SaveChangesAsync(ct);
    }

    public async Task RecalculateAllAsync(CancellationToken ct = default)
    {
        await using var session = store.LightweightSession();
        var groups = await session.Query<Group>().Where(g => !g.IsDeleted).ToListAsync(ct);
        var users = await session.Query<UserDocument>().ToListAsync(ct);

        foreach (var group in groups)
            RecomputeGroup(session, group, users);

        await session.SaveChangesAsync(ct);
    }

    // Full recompute of one group's materialized auto-members over the given user set. Manual groups
    // (and auto groups with no script) end up with an empty auto set — their membership is the explicit
    // email list, resolved at enforcement time.
    private void RecomputeGroup(IDocumentSession session, Group group, IReadOnlyList<UserDocument> users)
    {
        if (group.MembershipMode != MembershipMode.Auto || string.IsNullOrWhiteSpace(group.MembershipScript))
        {
            if (group.AutoMemberUserIds.Count > 0 || group.MembershipLastError is not null)
            {
                group.AutoMemberUserIds = [];
                group.MembershipLastError = null;
                session.Store(group);
            }
            return;
        }

        var matched = new List<Guid>();
        string? firstError = null;
        foreach (var user in users)
        {
            var result = evaluator.Evaluate(group.MembershipScript, GroupMembershipContext.FromUser(user));
            if (result.Error is { } error)
            {
                firstError ??= error;
                continue;
            }
            if (result.Matched)
                matched.Add(user.Id);
        }

        group.AutoMemberUserIds = matched;
        group.MembershipLastError = firstError;
        session.Store(group);
    }

    // Marten enum translation is avoided; group counts are small, so filter the mode in memory.
    private static async Task<List<Group>> LoadAutoGroupsAsync(IDocumentSession session, CancellationToken ct)
    {
        var groups = await session.Query<Group>().Where(g => !g.IsDeleted).ToListAsync(ct);
        return [.. groups.Where(g => g.MembershipMode == MembershipMode.Auto)];
    }
}
