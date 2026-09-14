using AlphaZero.Modules.Courses.Application.Courses.Commands.SyncResourceMetadata;
using MassTransit;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

/// <summary>
/// Base consumer for cross-module metadata change events.
/// Extracts metadata fields from the integration event, serializes them to a JsonElement,
/// and dispatches SyncResourceMetadataCommand to update the Courses module's materialized view.
/// </summary>
public abstract class MetadataChangedConsumerBase<TEvent> : IConsumer<TEvent>
    where TEvent : class
{
    private readonly ICoursesModule _coursesModule;

    protected MetadataChangedConsumerBase(ICoursesModule coursesModule)
    {
        _coursesModule = coursesModule;
    }

    /// <summary>
    /// Extract the source resource ID from the integration event.
    /// </summary>
    protected abstract Guid ExtractResourceId(TEvent message);

    /// <summary>
    /// Build an anonymous object containing the metadata fields to sync.
    /// The object will be serialized to a JsonElement for the SyncResourceMetadataCommand.
    /// </summary>
    protected abstract object BuildMetadataPayload(TEvent message);

    public async Task Consume(ConsumeContext<TEvent> context)
    {
        var msg = context.Message;
        var resourceId = ExtractResourceId(msg);
        var metadataJson = JsonSerializer.SerializeToElement(BuildMetadataPayload(msg));
        var command = new SyncResourceMetadataCommand(resourceId, metadataJson);
        await _coursesModule.Send(command, context.CancellationToken);
    }
}
