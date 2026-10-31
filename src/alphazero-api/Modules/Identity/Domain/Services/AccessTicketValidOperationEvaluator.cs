using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Security;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace AlphaZero.Modules.Identity.Domain.Services;

/// <summary>
/// The <see cref="Operator.AccessTicketValid"/> operator (issue #34 rework §3.3, step 6): a
/// course-agnostic, DATABASE-FREE gate that reads the access-window ticket from its own request
/// header (never the JWT) and verifies it through the shared signer — signature, ticket-exp, a
/// binding to the acting actor, and the frozen access window.
///
/// Design invariants (rework §3.3/§3.2.3):
/// - <b>fail closed</b>: missing header, empty header, unverifiable token, foreign ticket, elapsed
///   window — every non-verify shape DENIES;
/// - <b>no DB</b>: the operator reads token properties and the AuthorizationContext only (the
///   stateful mint gate in Courses is where enrollment status is checked);
/// - <b>course-agnostic</b>: the enrollment id in the payload is bound at mint time by the stateful
///   endpoint; the operator cannot (and must not) know which course the policy was written for.
/// </summary>
public class AccessTicketValidOperationEvaluator(
    IHttpContextAccessor httpContextAccessor,
    IAccessTicketSigner signer) : IOperationEvaluator
{
    public Operator EvaluatedOperator => Operator.AccessTicketValid;

    public Task<ErrorOr<Success>> Evaluate(object left, JsonElement right, AuthorizationContext context)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return Fail("Condition.HttpContextUnavailable", "HttpContext is not available (the access ticket travels with the request).");
        }

        var ticket = httpContext.Request.Headers[AccessTicketHttpHeader.Name].ToString();
        if (string.IsNullOrWhiteSpace(ticket))
        {
            // Fail closed (§3.3): course access without a ticket is not a policy NO-fact — it is a deny.
            return Fail("Condition.AccessTicketMissing",
                $"The '{AccessTicketHttpHeader.Name}' header is required for AccessTicketValid policies.");
        }

        if (!signer.TryVerifyTicket(ticket, out var payload, out var error))
        {
            return Fail("Condition.AccessTicketInvalid", $"Access ticket verification failed: {error}");
        }

        if (payload.UserId != context.Id
            || (!context.TenantId.HasValue || payload.TenantId != context.TenantId.Value))
        {
            return Fail("Condition.AccessTicketForeign", "The access ticket was minted for a different actor.");
        }

        if (!payload.IsWindowOpen(DateTimeOffset.UtcNow))
        {
            return Fail("Condition.AccessWindowClosed",
                "The entitlement's access window is closed (access may have expired — the window is re-checked at the stateful gate).");
        }

        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }

    private static Task<ErrorOr<Success>> Fail(string code, string description)
        => Task.FromResult<ErrorOr<Success>>(Error.Forbidden(code, description));
}
