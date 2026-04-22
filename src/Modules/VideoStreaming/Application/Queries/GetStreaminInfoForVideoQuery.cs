using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.VideoStreaming.Application.Queries;

public record StreamingInfoResponseDTO(
    string url, 
    string? encryptionMethod = "None",
    string? licenseUrl = null,
    DrmInfo? drm = null);

public record DrmInfo(
    string? widevineUrl = null,
    string? playReadyUrl = null,
    string? token = null);

public record GetStreaminInfoForVideoQuery(Guid VideoId) : IRequest<ErrorOr<StreamingInfoResponseDTO>>;

public sealed class GetStreaminInfoForVideoQueryHandler(IStreamingService streamingService) : IRequestHandler<GetStreaminInfoForVideoQuery, ErrorOr<StreamingInfoResponseDTO>>
{
    public async Task<ErrorOr<StreamingInfoResponseDTO>> Handle(GetStreaminInfoForVideoQuery request, CancellationToken cancellationToken)
    {
        var result = await streamingService.GetStreamingInfo(request.VideoId);
        return result;
    }
}

public interface IStreamingService
{
    Task<ErrorOr<StreamingInfoResponseDTO>> GetStreamingInfo(Guid videoId);
}