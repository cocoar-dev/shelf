using System.Text.Json;
using Cocoar.Shelf.Identity;
using Cocoar.Shelf.Models;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Endpoints;

public record TestSignInRequest(Guid? UserId, string? Email, string? DisplayName, bool? Admin);

/// <summary>
/// Test-only sign-in seam. Mapped ONLY when <c>ShelfOptions.TestAuth</c> is true — set by the
/// integration test fixture, never in dev-default or production. It replaces the removed local
/// password login as the tests' way to obtain a real <c>shelf.auth</c> cookie: the integration
/// suite can't reach the external modgud IdP. It is NOT a product login: it verifies no credential.
/// <c>UserId</c> signs in a known user; <c>Email</c> signs in a user, creating it on first use.
/// <c>Admin=true</c> stamps a modgud-shaped <c>resource_access</c> block carrying the configured
/// admin permission, so the genuine RBAC path (claims transformation → AdminCheck) is exercised.
/// </summary>
public static class TestAuthEndpoints
{
    public static RouteGroupBuilder MapTestAuthEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/test/signin", async (TestSignInRequest req, HttpContext ctx,
            UserManager<UserDocument> users, SignInManager<UserDocument> signIn, ShelfOptions options) =>
        {
            UserDocument? user;
            if (req.UserId is { } id)
            {
                user = await users.FindByIdAsync(id.ToString());
                if (user is null) return Results.NotFound();
            }
            else if (!string.IsNullOrWhiteSpace(req.Email))
            {
                user = await users.FindByEmailAsync(req.Email);
                user ??= await ModgudUserProvisioning.EnsureLocalUserAsync(
                    users, Guid.NewGuid(), req.Email, req.DisplayName ?? "Test");
            }
            else return Results.BadRequest("UserId or Email required");

            // Mirror the shape modgud emits so the whole flatten/check pipeline runs for real.
            string? resourceAccess = req.Admin == true
                ? JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    [options.Modgud.Audience] = new
                    {
                        roles = new[] { "Admin" },
                        permissions = new[] { options.Modgud.AdminPermission },
                    },
                })
                : null;

            await ModgudUserProvisioning.SignInWithRbacAsync(ctx, signIn, user, resourceAccess);
            return Results.Ok(new { user.Id, user.Email, user.DisplayName });
        });

        return api;
    }
}
