using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Security;
using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Application.Queries.GetVideoKey;

public class GetVideoKeyPreRequestHandler : IPreRequestHandler<GetVideoKeyQuery>
{
    private readonly IPlaybackTicketSigner _ticketSigner;
    private readonly IPolicyEvaluatorService _policyEvaluator;
    private readonly ITenantProvider _tenantProvider;

    public GetVideoKeyPreRequestHandler(
        IPlaybackTicketSigner ticketSigner,
        IPolicyEvaluatorService policyEvaluator,
        ITenantProvider tenantProvider)
    {
        _ticketSigner = ticketSigner;
        _policyEvaluator = policyEvaluator;
        _tenantProvider = tenantProvider;
    }

    public async Task<ErrorOr<Success>> PreHandle(
        GetVideoKeyQuery request,
        AuthorizationContext? context,
        CancellationToken cancellationToken)
    {
        // 1. If Ticket is provided, verify ticket (<0.05ms pure CPU crypto)
        if (!string.IsNullOrWhiteSpace(request.Ticket))
        {
            if (!_ticketSigner.TryVerifyTicket(request.Ticket, out var payload, out var error))
            {
                return Error.Forbidden("Ticket.InvalidOrExpired", error ?? "Playback ticket is invalid or expired.");
            }

            if (payload is null || payload.VideoId != request.VideoId)
            {
                return Error.Forbidden("Ticket.VideoMismatch", "Ticket video ID does not match the requested video.");
            }

            return Result.Success;
        }

        // 2. Fallback to IAM policy evaluation for teachers / administrators
        if (context is null)
        {
            return Error.Unauthorized("Identity.Unauthorized", "User is not authenticated.");
        }

        var tenantId = context.TenantId ?? _tenantProvider.GetTenant();
        if (tenantId is null || tenantId == Guid.Empty)
        {
            return Error.Unauthorized("Identity.Unauthorized", "Tenant context missing.");
        }

        var arn = ResourceArn.ForVideo(tenantId.Value, request.VideoId);
        var targetContext = context.ForResource("video:Stream", arn);
        var authResult = await _policyEvaluator.Authorize(targetContext);

        if (authResult.IsError)
        {
            return authResult.Errors;
        }

        return Result.Success;
    }
}
