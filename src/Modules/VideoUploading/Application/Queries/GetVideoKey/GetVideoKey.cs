using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.VideoUploading.Application.Queries.GetVideoKey;

public record GetVideoKeyQuery(Guid VideoId) : IRequest<ErrorOr<byte[]>>;

public sealed class GetVideoKeyQueryHandler(IRepository<VideoSecret> repo) : IRequestHandler<GetVideoKeyQuery, ErrorOr<byte[]>>
{
    public async Task<ErrorOr<byte[]>> Handle(GetVideoKeyQuery request, CancellationToken cancellationToken)
    {
        var secret = await repo.Entities.AsNoTracking()
            .FirstOrDefaultAsync(s => s.VideoId == request.VideoId, cancellationToken);

        if (secret == null)
        {
            return Error.NotFound("VideoSecret.NotFound", $"Secret for video {request.VideoId} not found.");
        }

        // Convert HEX back to bytes
        try
        {
            byte[] keyBytes = Convert.FromHexString(secret.KeyValue);
            return keyBytes;
        }
        catch (Exception)
        {
            return Error.Failure("VideoSecret.InvalidFormat", "Stored key is not a valid hex string.");
        }
    }
}
