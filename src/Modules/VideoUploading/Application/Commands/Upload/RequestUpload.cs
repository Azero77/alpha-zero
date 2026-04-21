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

namespace AlphaZero.Modules.VideoUploading.Application.Commands.Upload;

public record UploadCommand(string fileName, string contentType, string title, string? description, VideoTranscodingMetehod VideoTranscodingMetehod, VideoEncryptionMethod VideoEncryptionMethod = VideoEncryptionMethod.None): ICommand<UploadCommandResponse>;

public class UploadCommandValidator : AbstractValidator<UploadCommand>
{
    public UploadCommandValidator()
    {
        RuleFor(x => x.fileName)
            .NotEmpty()
            .Must(x => x.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only mp4 files are allowed.");

        RuleFor(x => x.contentType)
            .NotEmpty()
            .Must(x => x.Equals("video/mp4", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only video/mp4 content type is allowed.");

        RuleFor(x => x.title)
            .NotEmpty()
            .MaximumLength(255);
    }
}

public record UploadCommandResponse(Guid VideoId, Guid TenantId, string Key, string PreSignedUrl, string TranscodingMethod, string EncryptionMethod);

public sealed class UploadCommandHandler(IUploadService uploadService, IModuleBus moduleBus, IClock clock, ITenantProvider tenantProvider) : IRequestHandler<UploadCommand, ErrorOr<UploadCommandResponse>>
{
    public async Task<ErrorOr<UploadCommandResponse>> Handle(UploadCommand request, CancellationToken cancellationToken)
    {
        Guid? tenantId = tenantProvider.GetTenant();
        if (tenantId is null) return Error.Failure("Tenant.NotFound", "Tenant not found in context.");

        Guid videoId = Guid.NewGuid();
        var response = await uploadService.UploadFile(request.fileName, request.contentType, new Dictionary<string, string>()
        {
            { "VideoId" , videoId.ToString()},
            { "TenantId", tenantId.Value.ToString() },
            { "Title", request.title },
            { "Description", request.description ?? string.Empty },
            { "VideoTranscodingMetehod", request.VideoTranscodingMetehod.ToString() },
            { "VideoEncryptionMethod", request.VideoEncryptionMethod.ToString() }
        });
        if (response.IsError) return response.Errors;
        await moduleBus.Publish(new UploadVideoRequestedEvent(videoId, tenantId.Value, clock.Now, request.VideoEncryptionMethod.ToString()));

        return new UploadCommandResponse(
            videoId, 
            tenantId.Value, 
            response.Value.key, 
            response.Value.presignedUrl,
            request.VideoTranscodingMetehod.ToString(),
            request.VideoEncryptionMethod.ToString());
    }
}
