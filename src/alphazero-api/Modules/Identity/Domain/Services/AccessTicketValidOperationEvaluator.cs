using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.IntegrationEvents;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Security;
using ErrorOr;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
/// - <b>lazy expiry signal (§3.4 path a)</b>: when the verified ticket's window has elapsed, the
///   evaluation raises <see cref="PrincipalExpiredIntegrationEvent"/> — Identity's first
///   cross-module event — via the module bus (EF outbox on Identity's AppDbContext already
///   configured). The publish is <b>best-effort</b>: the DENY response always stands, publish
///   failures are logged, and consumed-side idempotency dedupes repeated evaluations.
/// </summary>
public class AccessTicketValidOperationEvaluator(
    IHttpContextAccessor httpContextAccessor,
    IAccessTicketSigner signer,
    IPublishEndpoint moduleBus,
    ILogger<AccessTicketValidOperationEvaluator> logger) : IOperationEvaluator
{
    public Operator EvaluatedOperator => Operator.AccessTicketValid;

    public async Task<ErrorOr<Success>> Evaluate(object left, JsonElement right, AuthorizationContext context)
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
            // §3.4 path (a): the operator sees the elapsed entitlement window — signal the lazy
            // mark to Courses (best-effort; duplicate evaluations are absorbed consumed-side).
            await TryRaisePrincipalExpired(context);

            return Fail("Condition.AccessWindowClosed",
                "The entitlement's access window is closed (access may have expired — the window is re-checked at the stateful gate).");
        }

        return Result.Success;
    }

    private async Task TryRaisePrincipalExpired(AuthorizationContext context)
    {
        try
        {
            if (!context.TenantId.HasValue)
            {
                logger.LogWarning(
                    "Access-window elapsed for actor {UserId} but the context carries no tenant — PrincipalExpired not raised.",
                    context.Id);
                return;
            }

            await moduleBus.Publish(new PrincipalExpiredIntegrationEvent(
                PrincipalId: context is PrincipalUserAuthorizationContext principal ? principal.PrincipalId : null,
                UserId: context.Id,
                TenantId: context.TenantId.Value,
                ResourcePath: context.ResourcePath));
        }
        catch (Exception ex)
        {
            // Best-effort by design (§3.4): the deny decision must never depend on bus health.
            // The scheduled sweep (rework §3.4 path c) is the guaranteed backstop.
            logger.LogError(ex,
                "Failed to raise PrincipalExpired for actor {UserId} (lesson: the scheduled sweep covers this window).",
                context.Id);
        }
    }

    private static ErrorOr<Success> Fail(string code, string description)
        => Error.Forbidden(code, description);
}
