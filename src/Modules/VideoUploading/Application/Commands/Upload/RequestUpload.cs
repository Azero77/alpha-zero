using AlphaZero.Modules.VideoUploading.Application.Services;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.Upload;

public record UploadCommand(string fileName, string contentType): IRequest<ErrorOr<UploadCommandResponse>>;

public record UploadCommandResponse(string key,string preSignedUrl);

public sealed class UploadCommandHandler(IUploadService uploadService) : IRequestHandler<UploadCommand, ErrorOr<UploadCommandResponse>>
{
    public async Task<ErrorOr<UploadCommandResponse>> Handle(UploadCommand request, CancellationToken cancellationToken)
    {

        //request policis , implemented later ......
        var response = await uploadService.UploadFile(request.fileName, request.contentType);

        if (response.IsError) return response.Errors;

        return new UploadCommandResponse(response.Value.key,response.Value.presignedUrl);
    }
}
