using AlphaZero.Modules.Assessments.IntegrationEvents;
using AlphaZero.Modules.Courses.Application.Courses.Commands.SyncResourceMetadata;
using MassTransit;
using MediatR;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

public class AssessmentMetadataChangedConsumer : IConsumer<AssessmentMetadataChangedIntegrationEvent>
{
    private readonly IMediator _mediator;

    public AssessmentMetadataChangedConsumer(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Consume(ConsumeContext<AssessmentMetadataChangedIntegrationEvent> context)
    {
        var msg = context.Message;

        // Use JsonSerializer to create a JsonElement snapshot
        var metadataJson = JsonSerializer.SerializeToElement(new
        {
            msg.Title,
            msg.Type,
            msg.PassingScore,
            msg.Status
        });

        var command = new SyncResourceMetadataCommand(msg.AssessmentId, metadataJson);
        
        // MediatR pipeline handles Unit of Work / SaveChanges
        await _mediator.Send(command, context.CancellationToken);
    }
}
