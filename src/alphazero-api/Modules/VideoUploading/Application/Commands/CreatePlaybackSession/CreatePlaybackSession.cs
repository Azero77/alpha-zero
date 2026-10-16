using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Security;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.CreatePlaybackSession;

public record EdgeCapabilityDto(
    string CookieName,
    string CookieValue,
    string? Domain,
    string Path,
    DateTimeOffset ExpiresAt,
    bool HttpOnly,
    bool Secure,
    string SameSite);

public record WatermarkContextDto(
    string UserId,
    string? UserName,
    string? UserDeviceid,
    string? userDeviceName,
    string TenantId,
    DateTimeOffset IssuedAt);

public record PlaybackSessionDto(
    Guid SessionId,
    Guid VideoId,
    string MasterManifestUrl,
    EdgeCapabilityDto EdgeCapability,
    WatermarkContextDto WatermarkContext);

public record CreatePlaybackSessionQuery(
    Guid VideoId,
    string? Ticket = null,
    Guid? CourseId = null,
    Guid? ItemId = null) : IQuery<PlaybackSessionDto>
{
    public CreatePlaybackSessionQuery(Guid videoId, Guid? courseId, Guid? itemId)
        : this(videoId, null, courseId, itemId)
    {
    }
}

public class CreatePlaybackSessionQueryValidator : AbstractValidator<CreatePlaybackSessionQuery>
{
    public CreatePlaybackSessionQueryValidator()
    {
        RuleFor(x => x.VideoId)
            .NotEmpty()
            .WithMessage("VideoId is required.");

        RuleFor(x => x.CourseId)
            .NotEmpty()
            .When(x => x.ItemId.HasValue)
            .WithMessage("CourseId is required when ItemId is provided.");
    }
}

public class CreatePlaybackSessionQueryHandler : IRequestHandler<CreatePlaybackSessionQuery, ErrorOr<PlaybackSessionDto>>
{
    private readonly IVideoRepository _videoRepository;
    private readonly ICloudflareCookieSigner _cookieSigner;
    private readonly IAuthorizationContextProvider _currentUserRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPlaybackTicketSigner? _ticketSigner;
    private readonly ILogger<CreatePlaybackSessionQueryHandler> _logger;

    public CreatePlaybackSessionQueryHandler(
        IVideoRepository videoRepository,
        ICloudflareCookieSigner cookieSigner,
        IAuthorizationContextProvider currentUserRepository,
        ITenantProvider tenantProvider,
        ILogger<CreatePlaybackSessionQueryHandler> logger)
        : this(videoRepository, cookieSigner, currentUserRepository, tenantProvider, null, logger)
    {
    }

    public CreatePlaybackSessionQueryHandler(
        IVideoRepository videoRepository,
        ICloudflareCookieSigner cookieSigner,
        IAuthorizationContextProvider currentUserRepository,
        ITenantProvider tenantProvider,
        IPlaybackTicketSigner? ticketSigner,
        ILogger<CreatePlaybackSessionQueryHandler> logger)
    {
        _videoRepository = videoRepository;
        _cookieSigner = cookieSigner;
        _currentUserRepository = currentUserRepository;
        _tenantProvider = tenantProvider;
        _ticketSigner = ticketSigner;
        _logger = logger;
    }

    public async Task<ErrorOr<PlaybackSessionDto>> Handle(
        CreatePlaybackSessionQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve User Context (Identity IAM or Capability Ticket)
        ErrorOr<AuthorizationContext?> currentUser =  _currentUserRepository.Get(cancellationToken);
        

        if (currentUser.IsError)
            return currentUser.Errors;
        if (currentUser.Value is null)
            return Error.Forbidden();
        var user = currentUser.Value;
            
        string userId = user.Id.ToString();
        string? userName = null ;
        string? userDeviceId = null;
        string? userDeviceName = null;
        Guid? userTenantId = user.TenantId;

        if (user is TenantUserAuthorizationContext tenantUser)
        {
            userName = tenantUser.Name;
            userDeviceId = tenantUser.DeviceId;
            userDeviceName = tenantUser.DeviceName;
        }

        // 2. Verify Video Existence and Readiness in VideoUploading DB
        var video = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
        if (video is null)
        {
            _logger.LogWarning("Video {VideoId} not found.", request.VideoId);
            return Error.NotFound("Video.NotFound", $"Video with ID '{request.VideoId}' was not found.");
        }

        if (video.Status != VideoStatus.Published)
        {
            _logger.LogWarning("Video {VideoId} is not in Published state (Status: {Status}).", request.VideoId, video.Status);
            return Error.Conflict("Video.NotReady", $"Video is not ready for playback (Current status: {video.Status}).");
        }

        // 3. Resolve Tenant and Mint Signed Edge Capability Cookie
        var tenantId = userTenantId ?? video.TenantId;
        var cookie = _cookieSigner.GenerateSignedCookie(tenantId, request.VideoId);

        // 4. Construct BFF Master Manifest Proxy Route
        var queryParams = new List<string>();
        if (request.CourseId.HasValue)
            queryParams.Add($"courseId={request.CourseId.Value:D}");
        if (request.ItemId.HasValue)
            queryParams.Add($"itemId={request.ItemId.Value:D}");

        var manifestUrl = queryParams.Count > 0
            ? $"/api/player/{request.VideoId:D}/master?{string.Join("&", queryParams)}"
            : $"/api/player/{request.VideoId:D}/master";

        // 5. Build DTO Response
        var edgeCapability = new EdgeCapabilityDto(
            CookieName: cookie.CookieName,
            CookieValue: cookie.CookieValue,
            Domain: cookie.Domain,
            Path: cookie.Path,
            ExpiresAt: cookie.ExpiresAt,
            HttpOnly: cookie.HttpOnly,
            Secure: cookie.Secure,
            SameSite: cookie.SameSite);

        var watermarkContext = new WatermarkContextDto(
            UserId: userId,
            UserName: userName,
            UserDeviceid: userDeviceId,
            userDeviceName: userDeviceName,
            TenantId: tenantId.ToString(),
            IssuedAt: DateTimeOffset.UtcNow);

        return new PlaybackSessionDto(
            SessionId: Guid.NewGuid(),
            VideoId: request.VideoId,
            MasterManifestUrl: manifestUrl,
            EdgeCapability: edgeCapability,
            WatermarkContext: watermarkContext);
    }
}
