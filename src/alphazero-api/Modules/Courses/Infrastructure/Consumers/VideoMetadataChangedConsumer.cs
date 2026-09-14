using AlphaZero.Modules.VideoUploading.IntegrationEvents;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

/// <summary>
/// Consumes VideoMetadataChangedIntegrationEvent from the VideoUploading module
/// and syncs the metadata to the Courses module's materialized CourseAsset view.
/// </summary>
public class VideoMetadataChangedConsumer : MetadataChangedConsumerBase<VideoMetadataChangedIntegrationEvent>
{
    public VideoMetadataChangedConsumer(ICoursesModule coursesModule)
        : base(coursesModule) { }

    protected override Guid ExtractResourceId(VideoMetadataChangedIntegrationEvent message)
        => message.VideoId;

    protected override object BuildMetadataPayload(VideoMetadataChangedIntegrationEvent message)
        => new { message.Title, message.Description };
}
