using AlphaZero.Modules.VideoUploading.Application.Queries;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.VideoUploading.Application.Queries.GetVideoKey;

public record GetVideoKeyQuery(Guid VideoId) : IRequest<ErrorOr<byte[]>>;

public sealed class GetVideoKeyQueryHandler(IVideoQueryService videoQueryService) : IRequestHandler<GetVideoKeyQuery, ErrorOr<byte[]>>
{
    public async Task<ErrorOr<byte[]>> Handle(GetVideoKeyQuery request, CancellationToken cancellationToken)
    {
        var secretValue = await videoQueryService.GetVideoSecretKeyAsync(request.VideoId, cancellationToken);

        if (secretValue == null)
        {
            return Error.NotFound("VideoSecret.NotFound", $"Secret for video {request.VideoId} not found.");
        }

        // Convert HEX back to bytes
        try
        {
            byte[] keyBytes = Convert.FromHexString(secretValue);
            return keyBytes;
        }
        catch (Exception)
        {
            return Error.Failure("VideoSecret.InvalidFormat", "Stored key is not a valid hex string.");
        }
    }
}
