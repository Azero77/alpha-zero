using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using Amazon.CloudFront.Model;
using Amazon.MediaConvert.Model;
using Aspire.Shared;
using ErrorOr;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.Process;

public record StartVideoTranscodingCommand(
    Guid VideoId,
    Guid TenantId, 
    string Key, 
    int SourceWidth,
    int SourceHeight,
    string? EncryptionMethod = "None") : ICommand<string>;

/// <summary>
/// Will implement the strategy pattern for the VideoTranscoding Service in order to select the proper transcoding service for the specified method, MediaConvert or FFMPEG
/// </summary>
public sealed class StartVideoTranscodingCommandHandler : IRequestHandler<StartVideoTranscodingCommand, ErrorOr<string>>
{
    private readonly IEnumerable<IVideoTranscodingService> _transcodingServices;
    private readonly AWSResources _aWSResources;
    private readonly ILogger<StartVideoTranscodingCommandHandler> _logger;
    private readonly IModuleBus _moduleBus;
    private readonly IUploadService _uploadService;

    public StartVideoTranscodingCommandHandler(
        IEnumerable<IVideoTranscodingService> transcodingServices,
        AWSResources aWSResources,
        ILogger<StartVideoTranscodingCommandHandler> logger,
        IModuleBus moduleBus,
        IUploadService uploadService)
    {
        _transcodingServices = transcodingServices;
        _aWSResources = aWSResources;
        _logger = logger;
        _moduleBus = moduleBus;
        _uploadService = uploadService;
    }

    public async Task<ErrorOr<string>> Handle(StartVideoTranscodingCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Application] Starting transcoding for Video: {VideoId} with Source Dimensions: {Width}x{Height}", 
            request.VideoId, request.SourceWidth, request.SourceHeight);

        string sourceBucket = _aWSResources.InputS3?.BucketName 
            ?? throw new ArgumentException("Input S3 bucket is not configured");

        string sourceS3 = VideoConstants.GetInputS3Url(sourceBucket,request.VideoId.ToString(), request.TenantId.ToString());
        string destinationBucket = _aWSResources.OutputS3?.BucketName 
            ?? throw new ArgumentException("Output S3 bucket is not configured");
        string outputPath = VideoConstants.GetOutputPathS3Url(destinationBucket, request.VideoId.ToString(), request.TenantId.ToString());

        var metadataResponse = await _uploadService.GetMetadata(request.Key);
        if (metadataResponse.IsError) return metadataResponse.Errors;

        var s3Metadata = metadataResponse.Value;
        if (!Enum.TryParse<VideoTranscodingMetehod>(s3Metadata.GetValueOrDefault("videotranscodingmetehod")?.ToString(), out var method))
        {
            return Error.Validation("VideoState.MethodNotFound", "The provided Transcoding Method is not supported");
        }

        if (!Enum.TryParse<VideoEncryptionMethod>(request.EncryptionMethod ?? s3Metadata.GetValueOrDefault("videoencryptionmethod")?.ToString(), out var encryptionMethod))
        {
             encryptionMethod = VideoEncryptionMethod.None;
        }

        var transcodingService = _transcodingServices.FirstOrDefault(s => s.Method == method);
        if (transcodingService is null)
            throw new InvalidOperationException("Trascoding Method service are not written yet");
        try
        {
            var jobIdResult = await transcodingService.StartTranscodingJobAsync(
                request.VideoId, 
                sourceS3, 
                outputPath, 
                request.SourceWidth, 
                request.SourceHeight, 
                encryptionMethod,
                cancellationToken);

            if (jobIdResult.IsError)
            {
                await _moduleBus.Publish(new VideoProcessingFailedEvent(request.VideoId, jobIdResult.FirstError.Description, request.Key));
                return jobIdResult.Errors;
            }

            return jobIdResult.Value;
        }
        catch (Exception ex)
        {
            await _moduleBus.Publish(new VideoProcessingFailedEvent(request.VideoId, ex.Message, request.Key));
            return Error.Failure("VideoUploading.Application.Failure", "Video Transcoding Failed");
        }
    }
}
