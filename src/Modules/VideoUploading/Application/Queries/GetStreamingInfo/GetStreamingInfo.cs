using AlphaZero.Modules.VideoUploading.Application.Repositories;
using Aspire.Shared;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.VideoUploading.Application.Queries.GetStreamingInfo;

public record StreamingInfo(string ManifestUrl, string? KeyId, string? Key);

public record GetStreamingInfoQuery(Guid VideoId) : IRequest<ErrorOr<StreamingInfo>>;

public sealed class GetStreamingInfoQueryHandler : IRequestHandler<GetStreamingInfoQuery, ErrorOr<StreamingInfo>>
{
    private readonly IVideoRepository _videoRepository;
    private readonly AWSResources _awsResources;

    public GetStreamingInfoQueryHandler(IVideoRepository videoRepository, AWSResources awsResources)
    {
        _videoRepository = videoRepository;
        _awsResources = awsResources;
    }

    public async Task<ErrorOr<StreamingInfo>> Handle(GetStreamingInfoQuery request, CancellationToken cancellationToken)
    {
        var video = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
        if (video == null)
        {
            return Error.NotFound("Video.NotFound", $"Video with ID {request.VideoId} was not found.");
        }

        if (video.Status != Domain.Models.VideoStatus.Published)
        {
            return Error.Failure("Video.NotPublished", "Video is not published yet.");
        }

        string bucketName = _awsResources.OutputS3?.BucketName ?? "alphazero-outputs3";
        // MediaConvert is naming the files after the VideoId based on your output
        // We switch to .mpd (DASH) because it has the best support for CMAF + ClearKey on web browsers
        string manifestUrl = $"https://{bucketName}.s3.eu-north-1.amazonaws.com/streaming/{video.Id}/{video.Id}.mpd";

        // For testing ClearKey, we use a deterministic key based on VideoId
        // MediaConvert StaticKeyProvider expects 32-character hex strings
        string keyId = video.Id.ToString("N");
        string key = video.Id.ToString("N"); // Using the same ID as the key for simplicity in this test

        return new StreamingInfo(manifestUrl, keyId, key);
    }
}
