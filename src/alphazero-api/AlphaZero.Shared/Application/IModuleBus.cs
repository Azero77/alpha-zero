using MassTransit;
using MassTransit.Mediator;

namespace AlphaZero.Shared.Application;

/// <summary>
/// A bus for dealing outside the application, sqs events from aws services most of the time. 
/// Handles Commands (Send), Events (Publish), and Queries (GetResponse) in-memory.
/// </summary>
public interface IExternalBus : IBus
{

}
