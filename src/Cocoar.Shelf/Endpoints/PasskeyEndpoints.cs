using System.Text;
using System.Text.Json;
using Cocoar.Shelf.Identity;
using Cocoar.Shelf.Models;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace Cocoar.Shelf.Endpoints;

public static class PasskeyEndpoints
{
    public static RouteGroupBuilder MapPasskeyEndpoints(this RouteGroupBuilder api)
    {
        var passkey = api.MapGroup("/auth/passkey");

        passkey.MapGet("/", ListPasskeys);
        passkey.MapPost("/register-options", RegisterOptions);
        passkey.MapPost("/register", Register);
        passkey.MapDelete("/{id:guid}", DeletePasskey);
        passkey.MapPost("/login-options", LoginOptions);
        passkey.MapPost("/login", LoginWithPasskey);

        return passkey;
    }

    private static async Task<IResult> ListPasskeys(
        HttpContext httpContext,
        UserManager<UserDocument> userManager,
        IQuerySession session,
        CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        var credentials = await session.Query<StoredPasskeyCredential>()
            .Where(c => c.UserId == user.Id)
            .ToListAsync(ct);

        return Results.Ok(credentials.Select(c => new
        {
            c.Id,
            c.DisplayName,
            c.CreatedAt,
            c.LastUsedAt
        }));
    }

    private static async Task<IResult> RegisterOptions(
        HttpContext httpContext,
        UserManager<UserDocument> userManager,
        IFido2 fido2,
        IQuerySession session,
        CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        var existingCredentials = await session.Query<StoredPasskeyCredential>()
            .Where(c => c.UserId == user.Id)
            .ToListAsync(ct);

        var excludeCredentials = existingCredentials
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToList();

        var fidoUser = new Fido2User
        {
            Id = user.Id.ToByteArray(),
            Name = user.UserName,
            DisplayName = user.DisplayName ?? user.UserName
        };

        var options = fido2.RequestNewCredential(
            new RequestNewCredentialParams
            {
                User = fidoUser,
                ExcludeCredentials = excludeCredentials,
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    ResidentKey = ResidentKeyRequirement.Preferred,
                    UserVerification = UserVerificationRequirement.Preferred
                },
                AttestationPreference = AttestationConveyancePreference.None
            });

        // Store challenge in a cookie (anonymous-safe)
        httpContext.Response.Cookies.Append("shelf.fido2.challenge",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(options.ToJson())),
            new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, MaxAge = TimeSpan.FromMinutes(5) });

        return Results.Ok(options);
    }

    private static async Task<IResult> Register(
        HttpContext httpContext,
        UserManager<UserDocument> userManager,
        IFido2 fido2,
        IDocumentStore store,
        CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        var challengeCookie = httpContext.Request.Cookies["shelf.fido2.challenge"];
        if (string.IsNullOrEmpty(challengeCookie))
            return Results.Json(new { error = "No pending registration" }, statusCode: 400);

        var options = CredentialCreateOptions.FromJson(
            Encoding.UTF8.GetString(Convert.FromBase64String(challengeCookie)));

        httpContext.Response.Cookies.Delete("shelf.fido2.challenge");

        var attestationResponse = await JsonSerializer.DeserializeAsync<AuthenticatorAttestationRawResponse>(
            httpContext.Request.Body, cancellationToken: ct);

        if (attestationResponse == null)
            return Results.Json(new { error = "Invalid attestation" }, statusCode: 400);

        var credential = await fido2.MakeNewCredentialAsync(
            new MakeNewCredentialParams
            {
                AttestationResponse = attestationResponse,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = async (args, _) =>
                {
                    await using var s = store.QuerySession();
                    var all = await s.Query<StoredPasskeyCredential>().ToListAsync(ct);
                    return !all.Any(c => c.CredentialId.SequenceEqual(args.CredentialId));
                }
            }, ct);

        var stored = new StoredPasskeyCredential
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CredentialId = credential.Id,
            PublicKey = credential.PublicKey,
            SignatureCounter = credential.SignCount,
            UserHandle = user.Id,
            DisplayName = $"Passkey {DateTimeOffset.UtcNow:yyyy-MM-dd}",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await using var session = store.LightweightSession();
        session.Store(stored);
        await session.SaveChangesAsync(ct);

        return Results.Created($"/_api/auth/passkey/{stored.Id}", new { stored.Id, stored.DisplayName });
    }

    private static async Task<IResult> DeletePasskey(
        Guid id,
        HttpContext httpContext,
        UserManager<UserDocument> userManager,
        IDocumentStore store,
        CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        await using var session = store.LightweightSession();
        var credential = await session.LoadAsync<StoredPasskeyCredential>(id, ct);

        if (credential == null || credential.UserId != user.Id)
            return Results.Json(new { error = "Passkey not found" }, statusCode: 404);

        session.Delete(credential);
        await session.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> LoginOptions(
        HttpContext httpContext,
        IFido2 fido2)
    {
        var options = fido2.GetAssertionOptions(
            new GetAssertionOptionsParams
            {
                AllowedCredentials = [],
                UserVerification = UserVerificationRequirement.Preferred
            });

        httpContext.Response.Cookies.Append("shelf.fido2.assertion",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(options.ToJson())),
            new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, MaxAge = TimeSpan.FromMinutes(5) });

        return Results.Ok(options);
    }

    private static async Task<IResult> LoginWithPasskey(
        HttpContext httpContext,
        IFido2 fido2,
        IDocumentStore store,
        UserManager<UserDocument> userManager,
        AppSignInManager signInManager,
        CancellationToken ct)
    {
        var assertionCookie = httpContext.Request.Cookies["shelf.fido2.assertion"];
        if (string.IsNullOrEmpty(assertionCookie))
            return Results.Json(new { error = "No pending login" }, statusCode: 400);

        var options = AssertionOptions.FromJson(
            Encoding.UTF8.GetString(Convert.FromBase64String(assertionCookie)));

        httpContext.Response.Cookies.Delete("shelf.fido2.assertion");

        var assertionResponse = await JsonSerializer.DeserializeAsync<AuthenticatorAssertionRawResponse>(
            httpContext.Request.Body, cancellationToken: ct);

        if (assertionResponse == null)
            return Results.Json(new { error = "Invalid assertion" }, statusCode: 400);

        await using var querySession = store.QuerySession();
        var assertionIdBytes = Convert.FromBase64String(assertionResponse.Id);
        var allCredentials = await querySession.Query<StoredPasskeyCredential>().ToListAsync(ct);
        var storedCred = allCredentials.FirstOrDefault(c => c.CredentialId.AsSpan().SequenceEqual(assertionIdBytes));

        if (storedCred == null)
            return Results.Json(new { error = "Unknown credential" }, statusCode: 401);

        var result = await fido2.MakeAssertionAsync(
            new MakeAssertionParams
            {
                AssertionResponse = assertionResponse,
                OriginalOptions = options,
                StoredPublicKey = storedCred.PublicKey,
                StoredSignatureCounter = storedCred.SignatureCounter,
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                    Task.FromResult(new Guid(args.UserHandle) == storedCred.UserId)
            }, ct);

        if (result == null)
            return Results.Json(new { error = "Verification failed" }, statusCode: 401);

        // Update signature counter
        await using var writeSession = store.LightweightSession();
        storedCred.SignatureCounter = result.SignCount;
        storedCred.LastUsedAt = DateTimeOffset.UtcNow;
        writeSession.Update(storedCred);
        await writeSession.SaveChangesAsync(ct);

        var user = await userManager.FindByIdAsync(storedCred.UserId.ToString());
        if (user == null || !user.IsActive)
            return Results.Json(new { error = "Account not found" }, statusCode: 401);

        // Passkey bypasses password + 2FA
        await signInManager.SignInAsync(user, isPersistent: true);
        return Results.Ok(new { ok = true, name = user.DisplayName ?? user.UserName });
    }
}
