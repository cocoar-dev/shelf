using Cocoar.Shelf.Models;
using Cocoar.Shelf.Services;
using Marten;

namespace Cocoar.Shelf.Endpoints;

/// <summary>
/// Admin lookup of assignable principals (groups + users) for the product access picker — both are
/// <see cref="IPrincipal"/>, so they come back as one kind-tagged list.
/// </summary>
public static class PrincipalEndpoints
{
    public static RouteGroupBuilder MapPrincipalEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/principals", GetPrincipals).RequireAuthorization("Admin");
        return api;
    }

    private static async Task<IResult> GetPrincipals(
        IGroupService groupService, IQuerySession session, CancellationToken ct)
    {
        var groups = await groupService.GetAllAsync();
        var users = await session.Query<UserDocument>().OrderBy(u => u.UserName).ToListAsync(ct);

        var principals = groups.Cast<IPrincipal>()
            .Concat(users.Where(u => !string.IsNullOrWhiteSpace(u.Email)).Cast<IPrincipal>())
            .Select(p => new
            {
                Kind = p.PrincipalKind.ToString(),
                Id = p.PrincipalId,
                DisplayName = p.PrincipalDisplayName,
            });

        return Results.Ok(principals);
    }
}
