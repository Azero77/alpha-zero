using AlphaZero.Modules.Assessments.IntegrationEvents;
using AlphaZero.Modules.Courses.Application.Courses.Commands.SyncResourceMetadata;
using MassTransit;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

public class AssessmentMetadataChangedConsumer : MetadataChangedConsumerBase<AssessmentMetadataChangedIntegrationEvent>
{
    public AssessmentMetadataChangedConsumer(ICoursesModule coursesModule)
        : base(coursesModule) { }

    protected override Guid ExtractResourceId(AssessmentMetadataChangedIntegrationEvent message)
        => message.AssessmentId;

    protected override object BuildMetadataPayload(AssessmentMetadataChangedIntegrationEvent message)
        => new
        {
            message.Title,
            message.Type,
            message.PassingScore,
            message.Status
        };
}
