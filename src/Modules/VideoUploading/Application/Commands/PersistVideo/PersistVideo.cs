using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.PersistVideo;

public record PersistVideoCommand(
    Guid VideoId, 
    TimeSpan Duration, 
    int Width, 
    int Height) : ICommand<Success>;

public sealed class PersistVideoCommandHandler : IRequestHandler<PersistVideoCommand, ErrorOr<Success>>
{
    private readonly ILogger<PersistVideoCommandHandler> _logger;
    private readonly IVideoRepository _videoRepository;
    private readonly IVideoStateRepository _stateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUploadService _uploadService;
    private readonly IClock _clock;

    public PersistVideoCommandHandler(
        ILogger<PersistVideoCommandHandler> logger,
        IVideoRepository videoRepository,
        IVideoStateRepository stateRepository,
        IUnitOfWork unitOfWork,
        IUploadService uploadService,
        IClock clock)
    {
        _logger = logger;
        _videoRepository = videoRepository;
        _stateRepository = stateRepository;
        _unitOfWork = unitOfWork;
        _uploadService = uploadService;
        _clock = clock;
    }

    public async Task<ErrorOr<Success>> Handle(PersistVideoCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Persisting analyzed video metadata for Video {VideoId}", request.VideoId);

        // 1. Fetch the transient state to get the Key and TenantId
        var videoState = await _stateRepository.GetByVideoIdAsync(request.VideoId, cancellationToken);
        if (videoState == null || videoState.Key == null)
        {
            return Error.NotFound("VideoState.NotFound", "Process state not found for this video.");
        }

        // 2. Idempotency Check
        var existingVideo = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
        if (existingVideo != null)
        {
            _logger.LogInformation("Video {VideoId} already exists. Skipping creation.", request.VideoId);
            return Result.Success;
        }

        // 3. Fetch S3 Metadata for file details
        var metadataResponse = await _uploadService.GetMetadata(videoState.Key);
        if (metadataResponse.IsError) return metadataResponse.Errors;

        var s3Metadata = metadataResponse.Value;
        string fileName = s3Metadata.GetValueOrDefault("file-name")?.ToString() ?? "Unknown";
        string title = s3Metadata.GetValueOrDefault("title")?.ToString() ?? fileName;
        string? description = s3Metadata.GetValueOrDefault("description")?.ToString();
        string contentType = s3Metadata.GetValueOrDefault("Content-Type")?.ToString() ?? "video/mp4";
        long fileSize = s3Metadata.TryGetValue("Content-Length", out var len) && long.TryParse(len.ToString(), out var l) ? l : 0;

        // 4. Create Domain Entity
        var videoResult = Video.Create(
            request.VideoId,
            videoState.TenantId,
            title,
            description,
            videoState.Key,
            new VideoMetadata(fileName, contentType, fileSize),
            _clock);

        if (videoResult.IsError) return videoResult.Errors;

        var video = videoResult.Value;
        
        // 5. Update Specifications from Analysis
        video.UpdateSpecifications(new VideoSpecifications(
            request.Duration, 
            new Resolution(request.Width, request.Height)));

        // 6. Persist
        await _videoRepository.AddAsync(video, cancellationToken);

        return Result.Success;
    }
}
