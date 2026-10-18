using System.Collections.Concurrent;
using MassTransit;

namespace Courses.Tests.Integration.Abstractions;

/// <summary>
/// Test-host publish observer (mirrors the fake-policy-evaluator precedent): connected to the
/// in-memory module bus by <see cref="ApiFactory"/> so tests can assert external bus behavior of
/// lifecycle events without changing how modules publish.
/// </summary>
public class RecordingPublishObserver : IPublishObserver
{
    public static readonly ConcurrentQueue<object> Messages = new();

    public static void Clear() => Messages.Clear();

    public static ConnectHandle ConnectOnce(IBus bus)
    {
        Messages.Clear();
        return bus.ConnectPublishObserver(new RecordingPublishObserver());
    }

    public Task PrePublish<T>(PublishContext<T> context) where T : class
    {
        Messages.Enqueue(context.Message!);
        return Task.CompletedTask;
    }

    public Task PostPublish<T>(PublishContext<T> context) where T : class => Task.CompletedTask;

    public Task PublishFault<T>(PublishContext<T> context, Exception exception) where T : class => Task.CompletedTask;
}
