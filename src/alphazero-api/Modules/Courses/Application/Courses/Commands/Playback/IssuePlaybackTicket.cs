using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Security;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
using Namotion.Reflection;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Playback;

public record IssuePlaybackTicketCommand(
    Guid CourseId,
    Guid CurriculumResourceId) : ICommand<IssuePlaybackTicketResult>;

public record IssuePlaybackTicketResult(
    string Ticket,
    Guid VideoId,
    int ExpiresInSeconds);

public sealed class IssuePlaybackTicketCommandHandler : IRequestHandler<IssuePlaybackTicketCommand, ErrorOr<IssuePlaybackTicketResult>>
{
    private const int TicketTtlSeconds = 120;

    private readonly ICourseResourceQueryService _courseResourceQueryService;
    private readonly IPlaybackTicketSigner _ticketSigner;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAuthorizationContextProvider _authorizationContextProvider;
    private readonly IDeviceProvider _deviceProvider;
    private readonly ILogger<IssuePlaybackTicketCommandHandler>? _logger;

    public IssuePlaybackTicketCommandHandler(
        IPlaybackTicketSigner ticketSigner,
        ITenantProvider tenantProvider,
        IAuthorizationContextProvider authorizationContextProvider,
        IDeviceProvider deviceProvider, ICourseResourceQueryService courseResourceQueryService, ILogger<IssuePlaybackTicketCommandHandler>? logger = null)
    {
        _ticketSigner = ticketSigner;
        _tenantProvider = tenantProvider;
        _authorizationContextProvider = authorizationContextProvider;
        _deviceProvider = deviceProvider;
        _courseResourceQueryService = courseResourceQueryService;
        _logger = logger;
    }

    public async Task<ErrorOr<IssuePlaybackTicketResult>> Handle(
        IssuePlaybackTicketCommand request, CancellationToken cancellationToken)
    {
        //No need to use Enrollement , because Policy Evaluator just needs a permission to course:view in this course to access the content,

        var authContext = _authorizationContextProvider.Get();

        if (authContext.IsError || authContext.Value is null) return Error.Forbidden();
        var userId = authContext.Value.Id;

        // 1. Fetch resolved details: curriculum placement (Layer 3) -> pool asset (Layer 2) -> external video id (Layer 1)
        var details = await _courseResourceQueryService.GetResourceDetailsAsync(
            request.CourseId, request.CurriculumResourceId, cancellationToken);

        if (details is null)
            return Error.NotFound("CurriculumResource.NotFound", "Curriculum resource not found in course.");

        if (details.ResourceType != ResourceType.Video)
            return Error.Validation("CurriculumItem.NoVideo", "Curriculum resource does not contain a valid video.");

        // 2. Validate tenant
        var tenantId = _tenantProvider.GetTenant();
        if (!tenantId.HasValue || tenantId == Guid.Empty)
        {
            return Error.Conflict("Tenant.Required", "Playback tickets cannot be issued globally."); //no global tickets are made
        }

        var ttl = TimeSpan.FromSeconds(TicketTtlSeconds);

        // 3. Mint capability ticket with the true external VideoId (Layer 1).
        //    The VideoUploading module verifies payload.VideoId == the actual video id,
        //    so passing the CourseAssetId (Layer 2) here would get streaming rejected.
        var ticket = _ticketSigner.GenerateTicket(
            tenantId.Value,
            userId,
            request.CourseId,
            details.CurriculumItemId,
            details.ExternalResourceId,
            ttl,
            _deviceProvider.GetDeviceId());

        _logger?.LogInformation(
            "Issued playback ticket for User {UserId}, Video {VideoId}, Course {CourseId}, Item {ItemId}.",
            userId, details.ExternalResourceId, request.CourseId, details.CurriculumItemId);

        return new IssuePlaybackTicketResult(ticket, details.ExternalResourceId, TicketTtlSeconds);
    }
}
