using ErrorOr;
using MediatR;
using AlphaZero.Modules.VideoUploading.Application.Services;
using FluentValidation;

namespace AlphaZero.Modules.VideoUploading.Application.Streaming.Queries;

public record GetVideoKeyQuery(Guid VideoId) : IRequest<ErrorOr<byte[]>>;

public class GetVideoKeyQueryValidation : AbstractValidator<GetVideoKeyQuery>
{
    public GetVideoKeyQueryValidation()
    {
        RuleFor(x => x.VideoId)
            .NotEmpty()
            .WithMessage("VideoId is required.");
    }
}

public class GetVideoKeyQueryHandler(IVideoEncryptionService encryptionService) 
    : IRequestHandler<GetVideoKeyQuery, ErrorOr<byte[]>>
{
    public async Task<ErrorOr<byte[]>> Handle(GetVideoKeyQuery request, CancellationToken cancellationToken)
    {
        return await encryptionService.GetClearKeyAsync(request.VideoId, cancellationToken);
    }
}
