using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Security;
using ErrorOr;
using FluentValidation;
using MassTransit;
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
    string UserName,
    string? UserPhone,
    string TenantId,
    DateTimeOffset IssuedAt);

public record PlaybackSessionDto(
    Guid SessionId,
    Guid VideoId,
    string MasterManifestUrl,
    EdgeCapabilityDto EdgeCapability,
    WatermarkContextDto WatermarkContext);

public record CreatePlaybackSessionCommand(
    Guid VideoId,
    Guid? CourseId = null,
    Guid? ItemId = null) : ICommand<PlaybackSessionDto>;

public class CreatePlaybackSessionCommandValidator : AbstractValidator<CreatePlaybackSessionCommand>
{
    public CreatePlaybackSessionCommandValidator()
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

public class CreatePlaybackSessionCommandHandler : IRequestHandler<CreatePlaybackSessionCommand, ErrorOr<PlaybackSessionDto>>
{
    private readonly IVideoRepository _videoRepository;
    private readonly ICloudflareCookieSigner _cookieSigner;
    private readonly ICurrentTenantUserRepository _currentUserRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IModuleBus _moduleBus;
    private readonly ILogger<CreatePlaybackSessionCommandHandler> _logger;

    public CreatePlaybackSessionCommandHandler(
        IVideoRepository videoRepository,
        ICloudflareCookieSigner cookieSigner,
        ICurrentTenantUserRepository currentUserRepository,
        ITenantProvider tenantProvider,
        IModuleBus moduleBus,
        ILogger<CreatePlaybackSessionCommandHandler> logger)
    {
        _videoRepository = videoRepository;
        _cookieSigner = cookieSigner;
        _currentUserRepository = currentUserRepository;
        _tenantProvider = tenantProvider;
        _moduleBus = moduleBus;
        _logger = logger;
    }

    public async Task<ErrorOr<PlaybackSessionDto>> Handle(
        CreatePlaybackSessionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve User Context
        var currentUser = await _currentUserRepository.GetCurrentUser();
        if (currentUser is null)
        {
            _logger.LogWarning("Playback session rejected: user is unauthenticated.");
            return Error.Unauthorized("Identity.Unauthorized", "User is not authenticated.");
        }

        // 2. If Course Context is provided, verify curriculum enrollment via Courses module
        if (request.CourseId.HasValue)
        {
            try
            {
                var requestClient = _moduleBus.CreateRequestClient<VerifyCoursePlaybackAccessRequest>();
                var verificationResponse = await requestClient.GetResponse<VerifyCoursePlaybackAccessResponse>(
                    new VerifyCoursePlaybackAccessRequest(
                        request.CourseId.Value,
                        request.ItemId,
                        request.VideoId,
                        currentUser.UserId),
                    cancellationToken);

                if (!verificationResponse.Message.IsAllowed)
                {
                    _logger.LogWarning(
                        "Course playback access denied for User {UserId} in Course {CourseId}: {Reason}",
                        currentUser.UserId, request.CourseId.Value, verificationResponse.Message.Reason);
                    return Error.Forbidden(
                        "Course.AccessDenied",
                        verificationResponse.Message.Reason ?? "Access to course video is denied.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error communicating with Courses module during access verification.");
                return Error.Failure("Courses.VerificationFailure", "Could not verify course access.");
            }
        }

        // 3. Verify Video Existence and Readiness in VideoUploading DB
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

        // 4. Resolve Tenant and Mint Signed Edge Capability Cookie
        var tenantId = _tenantProvider.GetTenant() ?? currentUser.TenantId ?? video.TenantId;
        var cookie = _cookieSigner.GenerateSignedCookie(tenantId, request.VideoId);

        // 5. Construct BFF Master Manifest Proxy Route
        var queryParams = new List<string>();
        if (request.CourseId.HasValue)
            queryParams.Add($"courseId={request.CourseId.Value:D}");
        if (request.ItemId.HasValue)
            queryParams.Add($"itemId={request.ItemId.Value:D}");

        var manifestUrl = queryParams.Count > 0
            ? $"/api/player/{request.VideoId:D}/master?{string.Join("&", queryParams)}"
            : $"/api/player/{request.VideoId:D}/master";

        // 6. Build DTO Response
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
            UserId: currentUser.UserId.ToString(),
            UserName: currentUser.Name,
            UserPhone: currentUser.DeviceName,
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
