using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.Domain.Services;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.Complete;

public record CompleteVideoProcessingCommand(Guid VideoId, string Key, Guid TenantId) : IRequest<ErrorOr<Success>>;

public sealed class CompleteVideoProcessingCommandHandler : IRequestHandler<CompleteVideoProcessingCommand, ErrorOr<Success>>
{
    private readonly ILogger<CompleteVideoProcessingCommandHandler> _logger;
    private readonly IVideoRepository _videoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUploadService _uploadService;
    private readonly IVideoSpecificationExtractorService _specExtractor;
    private readonly IClock _clock;
    private readonly IModuleBus _moduleBus;

    public CompleteVideoProcessingCommandHandler(
        ILogger<CompleteVideoProcessingCommandHandler> logger,
        IVideoRepository videoRepository,
        IUnitOfWork unitOfWork,
        IUploadService uploadService,
        IVideoSpecificationExtractorService specExtractor,
        IClock clock,
        IModuleBus moduleBus)
    {
        _logger = logger;
        _videoRepository = videoRepository;
        _unitOfWork = unitOfWork;
        _uploadService = uploadService;
        _specExtractor = specExtractor;
        _clock = clock;
        _moduleBus = moduleBus;
    }

    public async Task<ErrorOr<Success>> Handle(CompleteVideoProcessingCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Completing video processing for Video {VideoId}", request.VideoId);

        // Idempotency check: if video already exists, we might have already processed this completion
        var existingVideo = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
        if (existingVideo != null)
        {
            _logger.LogInformation("Video {VideoId} already exists in database. Skipping creation.", request.VideoId);
            // Ensure the integration event is published if it was missed
            await _moduleBus.Publish(new VideoPublishedEvent(request.VideoId), cancellationToken);
            return Result.Success;
        }

        var metadataResponse = await _uploadService.GetMetadata(request.Key);
        if (metadataResponse.IsError)
        {
            _logger.LogError("Failed to get metadata for video {VideoId}", request.VideoId);
            return metadataResponse.Errors;
        }

        var metadata = metadataResponse.Value;
        string fileName = metadata.GetValueOrDefault("file-name")?.ToString() ?? "Unknown";
        string contentType = metadata.GetValueOrDefault("Content-Type")?.ToString() ?? "video/mp4";
        
        long fileSize = 0;
        if (metadata.TryGetValue("Content-Length", out var lengthObj) && long.TryParse(lengthObj.ToString(), out var length))
        {
            fileSize = length;
        }

        var videoMetadata = new VideoMetadata(fileName, contentType, fileSize);
        
        var videoResult = Video.Create(
            request.VideoId,
            request.TenantId,
            fileName,
            null,
            request.Key,
            videoMetadata,
            _clock);

        if (videoResult.IsError)
        {
            _logger.LogError("Failed to create Video entity: {Error}", videoResult.FirstError.Description);
            return videoResult.Errors;
        }

        var video = videoResult.Value;
        
        var specResult = await _specExtractor.ExtractAsync(video, cancellationToken);
        if (specResult.IsError)
        {
            _logger.LogWarning("Failed to extract specifications for Video {VideoId}: {Error}", request.VideoId, specResult.FirstError.Description);
            video.MarkAsFailed();
        }
        else
        {
            string outputFolder = $"streaming/{request.VideoId}/";
            video.MarkAsPublished(outputFolder, specResult.Value, _clock);
        }

        await _videoRepository.AddAsync(video, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _moduleBus.Publish(new VideoPublishedEvent(request.VideoId), cancellationToken);

        return Result.Success;
    }
}