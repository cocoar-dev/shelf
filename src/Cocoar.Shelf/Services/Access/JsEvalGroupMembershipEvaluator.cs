using System.Threading;
using Cocoar.JsEval.Engine;
using Jint.Runtime;

namespace Cocoar.Shelf.Services.Access;

/// <summary>
/// Sandboxed JsEval implementation (Cocoar.JsEval, same engine as timetodo). The predicate is a JS
/// boolean expression over a global <c>user</c> object built from the persisted claims snapshot, e.g.
/// <c>user.email.endsWith('@cocoar.dev')</c> or <c>user.claims.groups?.includes('engineering')</c>.
///
/// <para>Fail-closed: any syntax error, script throw, execution timeout or non-boolean result yields
/// false — a broken predicate never grants access.</para>
/// </summary>
public sealed partial class JsEvalGroupMembershipEvaluator(
    JsEngine engine, ILogger<JsEvalGroupMembershipEvaluator> logger) : IGroupMembershipEvaluator
{
    // The facade's underlying Jint engine isn't thread-safe and we mutate a `user` global per
    // evaluation. Recomputes are infrequent (login / group save / manual), so serialize all
    // evaluations app-wide rather than pooling engines.
    private static readonly Lock Gate = new();

    public GroupPredicateResult Evaluate(string script, GroupMembershipContext user)
    {
        if (string.IsNullOrWhiteSpace(script))
            return new GroupPredicateResult(false, "Membership script is empty.");

        var userObject = BuildUserObject(user);
        try
        {
            lock (Gate)
            {
                var jint = engine.UnderlyingEngine;
                jint.SetValue("user", userObject);
                // TypeConverter.ToBoolean applies JS truthy coercion, so an undefined/null/missing-claim
                // result is well-defined and safe (→ false) without needing a Boolean(...) wrapper.
                var result = jint.Evaluate(script);
                return new GroupPredicateResult(TypeConverter.ToBoolean(result), null);
            }
        }
        catch (Exception ex)
        {
            LogPredicateFailed(logger, ex);
            return new GroupPredicateResult(false, ex.Message);
        }
    }

    // Jint exposes an IDictionary<string, object> as a JS object whose keys are members, so lowercase
    // keys here give the `user.email` / `user.permissions` / `user.claims.x` access the scripts expect.
    private static Dictionary<string, object?> BuildUserObject(GroupMembershipContext user)
    {
        var claims = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, values) in user.Claims)
            claims[key] = values.Length == 1 ? values[0] : values;

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["email"] = user.Email,
            ["permissions"] = user.Permissions.ToArray(),
            ["claims"] = claims,
        };
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Group auto-membership predicate failed to evaluate; treating as no-match")]
    private static partial void LogPredicateFailed(ILogger logger, Exception ex);
}
