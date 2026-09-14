using MassTransit;
using MassTransit.Mediator;

namespace AlphaZero.Shared.Application;

/// <summary>
/// A unified cross-module communication bus. 
/// Handles Commands (Send), Events (Publish), and Queries (GetResponse) in-memory.
/// TODO (Broker Selection): When transitioning to a dedicated external broker (e.g. AWS SNS/SQS or RabbitMQ),
/// configure topic-based routing using TargetResourceArn segments (e.g., "courses.video", "tenant.video")
/// so module queues subscribe only to topics relevant to their domain.
/// </summary>
public interface IModuleBus : IBus
{
}


/// <summary>
/// A bus for dealing outside the application, sqs events from aws services most of the time. 
/// Handles Commands (Send), Events (Publish), and Queries (GetResponse) in-memory.
/// </summary>
public interface IExternalBus : IBus
{

}