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
    Guid VideoResourceId) : ICommand<IssuePlaybackTicketResult>;

public record IssuePlaybackTicketResult(
    string Ticket,
    Guid VideoId,
    int ExpiresInSeconds);

public sealed class IssuePlaybackTicketCommandHandler : IRequestHandler<IssuePlaybackTicketCommand, ErrorOr<IssuePlaybackTicketResult>>
{
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
        // 1. Verify active enrollment

        var authContext = _authorizationContextProvider.Get();

        if (authContext.IsError || authContext.Value is null) return Error.Forbidden();
        var userId = authContext.Value.Id;
        // 3. Extract VideoCurriculumResource
        var courseResource = await _courseResourceQueryService.GetResourceInCourse(request.CourseId, request.VideoResourceId, cancellationToken);
        
        if (courseResource is null || courseResource.ResourceType != ResourceType.Video )
        {
            return Error.Validation("CurriculumItem.NoVideo", "Curriculum item does not contain a valid video resource.");
        }
        //TODO make the request to db get the video by using CourseId 

        // 4. Mint stateless capability ticket
        var tenantId = _tenantProvider.GetTenant(); // WE ARE SURE THAT THE USER here is authenticated with a tenant
        if ( tenantId.HasValue || tenantId == Guid.Empty)
            return Error.Conflict(""); //no global tickets are made
        const int ticketTtlSeconds = 120;
        var ttl = TimeSpan.FromSeconds(ticketTtlSeconds);

        var ticket = _ticketSigner.GenerateTicket(
            tenantId!.Value,
            authContext.Value.Id,
            request.CourseId,
            courseResource.ItemId,
            courseResource.AssetId!.Value,
            ttl,
            _deviceProvider.GetDeviceId());

        _logger?.LogInformation(
            "Issued playback ticket for User {UserId}, Video {VideoId}, Course {CourseId}, Item {ItemId}.",
            authContext.Value.Id, courseResource.AssetId!.Value, request.CourseId, courseResource.ItemId);

        return new IssuePlaybackTicketResult(ticket, courseResource.AssetId!.Value, ticketTtlSeconds);
    }
}
