using AlphaZero.Modules.Documents.IntegrationEvents;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

/// <summary>
/// Consumes DocumentMetadataChangedIntegrationEvent from the Documents module
/// and syncs the metadata to the Courses module's materialized CourseAsset view.
/// </summary>
public class DocumentMetadataChangedConsumer : MetadataChangedConsumerBase<DocumentMetadataChangedIntegrationEvent>
{
    public DocumentMetadataChangedConsumer(ICoursesModule coursesModule)
        : base(coursesModule) { }

    protected override Guid ExtractResourceId(DocumentMetadataChangedIntegrationEvent message)
        => message.DocumentId;

    protected override object BuildMetadataPayload(DocumentMetadataChangedIntegrationEvent message)
        => new
        {
            message.Title,
            message.Description,
            FileSize = message.FileSizeBytes,
            ContentType = message.FileType
        };
}
