using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using Aspire.Shared;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.Process;

public record StartVideoTranscodingCommand(Guid VideoId, string Key, string BucketName) : IRequest<ErrorOr<string>>;

public sealed class StartVideoTranscodingCommandHandler : IRequestHandler<StartVideoTranscodingCommand, ErrorOr<string>>
{
    private readonly IVideoTranscodingService _transcodingService;
    private readonly AWSResources _aWSResources;
    private readonly ILogger<StartVideoTranscodingCommandHandler> _logger;
    private readonly IModuleBus _moduleBus;
    public StartVideoTranscodingCommandHandler(
        IVideoTranscodingService transcodingService,
        AWSResources aWSResources,
        ILogger<StartVideoTranscodingCommandHandler> logger,
        IModuleBus moduleBus)
    {
        _transcodingService = transcodingService;
        _aWSResources = aWSResources;
        _logger = logger;
        _moduleBus = moduleBus;
    }

    public async Task<ErrorOr<string>> Handle(StartVideoTranscodingCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Application] Starting transcoding for Video: {VideoId}", request.VideoId);

        string sourceS3 = $"s3://{request.BucketName}/{request.Key}";
        string destinationBucket = _aWSResources.OutputS3?.BucketName 
            ?? throw new ArgumentException("Output S3 bucket is not configured");
        string outputPath = $"s3://{destinationBucket}/streaming/{request.VideoId}/";

        try
        {
            return await _transcodingService.StartTranscodingJobAsync(request.VideoId, sourceS3, outputPath, cancellationToken);
        }
        catch (Exception)
        {
            await _moduleBus.Publish(new VideoUploadFailedEvent(request.VideoId,request.Key));
            return Error.Failure("VideoUploading.Application.Failure","Video Transcoding Failed");
        }
    }
}