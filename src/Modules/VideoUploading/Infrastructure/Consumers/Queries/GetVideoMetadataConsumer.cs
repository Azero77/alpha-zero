using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using MassTransit;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers.Queries;

public class GetVideoMetadataConsumer : IConsumer<GetVideoMetadataRequest>
{
    private readonly IVideoRepository _videoRepository;

    public GetVideoMetadataConsumer(IVideoRepository videoRepository)
    {
        _videoRepository = videoRepository;
    }

    public async Task Consume(ConsumeContext<GetVideoMetadataRequest> context)
    {
        var video = await _videoRepository.GetById(context.Message.VideoId);
        
        if (video == null)
        {
            await context.RespondAsync(new VideoMetaDataNotFoundResponse(context.Message.VideoId));
            return;
        }

        await context.RespondAsync(new VideoMetadataResponse(
            video.Id,
            video.Title,
            video.Description,
            video.Status.ToString(),
            video.Duration.HasValue ? video.Duration.Value.ToString(@"hh\:mm\:ss") : null,
            video.RelativeUrl));
    }
}
