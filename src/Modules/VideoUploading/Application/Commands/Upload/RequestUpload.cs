using AlphaZero.Modules.VideoUploading.Application.Services;
using ErrorOr;
using MediatR;
using MassTransit;
using MassTransit.Mediator;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using FluentValidation;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.Upload;

public record UploadCommand(string fileName, string contentType): IRequest<ErrorOr<UploadCommandResponse>>;

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
    }
}

public record UploadCommandResponse(Guid VideoId, string Key,string PreSignedUrl);

public sealed class UploadCommandHandler(IUploadService uploadService, IModuleBus moduleBus, IClock clock) : IRequestHandler<UploadCommand, ErrorOr<UploadCommandResponse>>
{
    public async Task<ErrorOr<UploadCommandResponse>> Handle(UploadCommand request, CancellationToken cancellationToken)
    {
        Guid videoId = Guid.NewGuid();
        //request policis , implemented later ......
        var response = await uploadService.UploadFile(request.fileName, request.contentType, new Dictionary<string, string>()
        {
            { "VideoId" , videoId.ToString()}
        });
        if (response.IsError) return response.Errors;
        await moduleBus.Publish(new UploadVideoRequestedEvent(videoId,clock.Now));

        return new UploadCommandResponse(videoId,response.Value.key,response.Value.presignedUrl);
    }
}
