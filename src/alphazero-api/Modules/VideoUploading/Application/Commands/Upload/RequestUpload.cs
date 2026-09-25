using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using Amazon.Runtime.Internal.Transform;
using ErrorOr;
using FluentValidation;
using MassTransit;
using MassTransit.Mediator;
using MediatR;
using System.Data;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.Upload;

public record UploadCommand(
    string FileName, 
    string ContentType, 
    string Title, 
    string? Description, 
    string TargetResourceArn,
    string VideoTranscodingMethod, 
    string VideoEncryptionMethod,
    UploadThumbnailCommand? UploadThumbnail
    ): ICommand<UploadCommandResponse>;

public record UploadThumbnailCommand(string FileName, string ContentType);

public class UploadCommandValidator : AbstractValidator<UploadCommand>
{
    public UploadCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .Must(x => x.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only mp4 files are allowed.");

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .Must(x => x.Equals("video/mp4", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only video/mp4 content type is allowed.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.VideoTranscodingMethod)
            .IsEnumName(typeof(VideoTranscodingMetehod), caseSensitive: false)
            .WithMessage("Invalid video transcoding method.");
        RuleFor(x => x.VideoEncryptionMethod)
            .IsEnumName(typeof(VideoEncryptionMethod), caseSensitive: false)
            .WithMessage("Invalid video encryption method.");
        RuleFor(x => x.UploadThumbnail)
            .Must(yy =>
            {

                if(yy is not null)
                {
                    //checking the extensions 
                    return VideoConstants.AllowedThumbnailMIMETypes.Contains(yy.ContentType,StringComparer.OrdinalIgnoreCase) && VideoConstants.AllowedThumbnailExtensions.Any(extension => yy.FileName.EndsWith(extension,StringComparison.OrdinalIgnoreCase));
                }
                return true;
            });
        RuleFor(x => x.TargetResourceArn)
            .NotEmpty()
            .NotNull()
            .Must(y =>  ResourceArn.TryParse(y,out _));
    }
}

public record UploadCommandResponse(
    Guid VideoId, 
    Guid TenantId, 
    string Key, 
    string PreSignedUrl, 
    string TranscodingMethod, 
    string EncryptionMethod, 
    Dictionary<string, string> Headers,
    string? ThumbnailKey = null,
    string? ThumbnailPreSignedUrl = null,
    Dictionary<string, string>? ThumbnailHeaders = null);

public sealed class UploadCommandHandler(
    IUploadService uploadService,
    IVideoRepository videoRepository,
    IModuleBus moduleBus,
    IClock clock,
    ITenantProvider tenantProvider) : IRequestHandler<UploadCommand, ErrorOr<UploadCommandResponse>>
{
    public async Task<ErrorOr<UploadCommandResponse>> Handle(UploadCommand request, CancellationToken cancellationToken)
    {
        Guid? tenantId = tenantProvider.GetTenant();
        if (tenantId is null) return Error.Failure("Tenant.NotFound", "Tenant not found in context.");

        Guid videoId = Guid.NewGuid();
        
        var response = await uploadService.UploadFile(request.FileName,VideoConstants.GetInputVideoSourceKey(videoId.ToString(), tenantId.ToString()!) ,request.ContentType, new Dictionary<string, string>()
        {
            { "VideoId" , videoId.ToString()},
            { "TenantId", tenantId.Value.ToString() },
            { "Title", request.Title },
            { "Description", request.Description ?? string.Empty },
            { "VideoTranscodingMetehod", request.VideoTranscodingMethod.ToString() },
            { "VideoEncryptionMethod", request.VideoEncryptionMethod.ToString() },
            { "TargetResourceArn", request.TargetResourceArn}
        });
        if (response.IsError) return response.Errors;

        string? thumbnailKey = null;
        string? thumbnailPreSignedUrl = null;
        Dictionary<string, string>? thumbnailHeaders = null;

        if (request.UploadThumbnail is not null)
        {
            string thumbnailExtension = Path.GetExtension(request.UploadThumbnail.FileName);
            var thumbResponse = await uploadService.UploadFile(request.UploadThumbnail.FileName, VideoConstants.GetThumbnailInputVideoSourceKey(videoId.ToString(), tenantId.ToString()!, thumbnailExtension),request.UploadThumbnail.ContentType, new Dictionary<string, string>()
            {
                { "VideoId" , videoId.ToString()},
                { "TenantId", tenantId.Value.ToString() },
                { "ContentType", request.UploadThumbnail.ContentType},
                { "FileName", request.UploadThumbnail.FileName},
                { "IsThumbnail", "true" }
            });
            if(thumbResponse.IsError)
                return thumbResponse.Errors;
            thumbnailKey = thumbResponse.Value.key;
            thumbnailPreSignedUrl = thumbResponse.Value.presignedUrl;
            thumbnailHeaders = thumbResponse.Value.headers;
        }

        var thumbnail = new ThumbnailInfo(thumbnailKey, null, thumbnailKey != null);
        var videoResult = Video.Create(
            videoId,
            tenantId.Value,
            request.Title,
            request.Description,
            new VideoMetadata(request.FileName, request.ContentType, 0, request.VideoTranscodingMethod, request.VideoEncryptionMethod),
            thumbnail,
            clock);

        if (videoResult.IsError) return videoResult.Errors;

        await videoRepository.AddAsync(videoResult.Value, cancellationToken);

        await moduleBus.Publish(new UploadVideoRequestedEvent(
            videoId, 
            tenantId.Value, 
            clock.Now, 
            request.VideoEncryptionMethod.ToString(),
            thumbnailKey,
            request.TargetResourceArn),
            cancellationToken);

        return new UploadCommandResponse(
            videoId, 
            tenantId.Value, 
            response.Value.key, 
            response.Value.presignedUrl, 
            request.VideoTranscodingMethod.ToString(),
            request.VideoEncryptionMethod.ToString(),
            response.Value.headers,
            thumbnailKey,
            thumbnailPreSignedUrl,
            thumbnailHeaders);
    }
}