using System.Collections.Concurrent;
using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Shared.Domain;
using MassTransit;
using MassTransit.Testing;
using MassTransit.Testing.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Sagas;

/// <summary>
/// Base harness for driving MassTransit saga state machines with the DI test harness.
/// The module's real consumers that fulfill a saga's outgoing Send are replaced by
/// test forwarders: they record every received outgoing command (assertable) and
/// publish the event the real consumer would publish so the machine progresses
/// through its transitions (EnrollStudentFromSagaConsumerTests style).
/// </summary>
public abstract class SagaHarnessTestBase<TMachine, TInstance> : IAsyncLifetime
    where TMachine : MassTransitStateMachine<TInstance>
    where TInstance : class, SagaStateMachineInstance
{
    static SagaHarnessTestBase()
    {
        SagaTestConventions.EnsureMapped();

        // The in-memory transport round-trips messages through the MassTransit System.Text.Json
        // serializer; ResourceArn is an immutable value object with private constructors, so it
        // needs a custom converter (mirrors Modules/Identity/Infrastructure/Serialization/Converters.cs).
        MassTransit.Serialization.SystemTextJsonMessageSerializer.Options.Converters.Add(
            new TestResourceArnJsonConverter());
    }

    public SagaHarnessTestBase() { }

    protected ServiceProvider Provider = null!;
    protected ITestHarness Harness = null!;
    protected TMachine Machine = null!;
    protected TimeSpan TestStateTimeout { get; } = TimeSpan.FromSeconds(30);

    protected abstract void ConfigureSaga(IBusRegistrationConfigurator configurator);

    /// <summary>Per-harness recorder of the outgoing saga commands received by the test forwarders.</summary>
    protected ForwarderRecorder Recorder = null!;

    public async Task InitializeAsync()
    {
        Recorder = new ForwarderRecorder();

        var services = new ServiceCollection();
        services.AddSingleton(Recorder);
        services.AddMassTransitTestHarness(cfg => { ConfigureSaga(cfg); });

        Provider = services.BuildServiceProvider(true);
        Harness = Provider.GetRequiredService<ITestHarness>();
        Machine = Provider.GetRequiredService<TMachine>();
        await Harness.Start();
    }

    public async Task DisposeAsync()
    {
        if (Provider is not null)
            await Provider.DisposeAsync();
    }

    protected ISagaStateMachineTestHarness<TMachine, TInstance> MachineHarness()
        => Harness.GetSagaStateMachineHarness<TMachine, TInstance>();

    /// <summary>Polls until the condition holds (in-memory transport delivery is asynchronous).</summary>
    protected static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var giveUpAt = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition() && DateTime.UtcNow < giveUpAt)
            await Task.Delay(10);
    }
}

/// <summary>
/// Maps the saga outgoing commands to the fixed test-consumer endpoint addresses via
/// the static EndpointConvention cache (same mechanism MassTransit's convention-based
/// saga .Send(...) relies on in production - see the issue #35 reconciliation comment
/// about nobody calling EndpointConvention.Map in the host).
/// </summary>
public static class SagaTestConventions
{
    private static int _mapped;

    public static void EnsureMapped()
    {
        if (System.Threading.Interlocked.Exchange(ref _mapped, 1) == 1)
            return;

        EndpointConvention.Map<EnrollStudentFromSagaCommand>(new Uri("loopback://localhost/EnrollForwarder"));
        EndpointConvention.Map<AssignStudentRoleFromSagaCommand>(new Uri("loopback://localhost/AssignRoleForwarder"));
        EndpointConvention.Map<RevokeStudentEnrollmentFromSagaCommand>(new Uri("loopback://localhost/RevokeForwarder"));
        EndpointConvention.Map<RemoveStudentRoleFromSagaCommand>(new Uri("loopback://localhost/RemoveRoleForwarder"));
    }
}

/// <summary>
/// JSON round-trip for ResourceArn on the MassTransit System.Text.Json serializer
/// (same shape as the Identity module converter: ARN value as a JSON string).
/// </summary>
public class TestResourceArnJsonConverter : System.Text.Json.Serialization.JsonConverter<ResourceArn>
{
    public override ResourceArn Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
        => ResourceArn.Create(reader.GetString()!).Value;

    public override void Write(System.Text.Json.Utf8JsonWriter writer, ResourceArn value, System.Text.Json.JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

/// <summary>Thread-safe per-harness record of received outgoing saga commands, keyed by correlation id.</summary>
public class ForwarderRecorder
{
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<Guid, List<object>>> _received = new();

    public void Reset() => _received.Clear();

    public void Record(Type consumerType, Guid correlationId, object message)
    {
        var bag = _received.GetOrAdd(consumerType, _ => new ConcurrentDictionary<Guid, List<object>>());
        var list = bag.GetOrAdd(correlationId, _ => new List<object>());
        lock (list)
        {
            list.Add(message);
        }
    }

    /// <summary>Snapshot of all messages of <typeparamref name="T"/> recorded for the given consumer message type.</summary>
    public List<T> Received<T>()
        where T : class
    {
        if (!_received.TryGetValue(typeof(T), out var bag))
            return new List<T>();

        var all = new List<object>();
        foreach (var kvp in bag)
        {
            lock (kvp.Value)
            {
                all.AddRange(kvp.Value);
            }
        }
        return all.OfType<T>().ToList();
    }
}

/// <summary>
/// Records the outgoing saga command when the real (module) consumer would receive it.
/// Test methods progress the state machine by publishing the continuation events
/// themselves, so every intermediate state is deterministically observable.
/// The recorder is injected per test-harness instance (parallel test classes must
/// not observe each other's command traffic).
/// </summary>
public abstract class ForwarderBase<TCommand> : IConsumer<TCommand>
    where TCommand : class
{
    private readonly ForwarderRecorder _recorder;

    protected ForwarderBase(ForwarderRecorder recorder) => _recorder = recorder;

    public Task Consume(ConsumeContext<TCommand> context)
    {
        _recorder.Record(typeof(TCommand), GetCorrelationId(context.Message), context.Message);
        return Task.CompletedTask;
    }

    protected abstract Guid GetCorrelationId(TCommand message);
}

/// <summary>Simulates EnrollStudentFromSagaConsumer.</summary>
public class EnrollForwarder : ForwarderBase<EnrollStudentFromSagaCommand>
{
    public EnrollForwarder(ForwarderRecorder recorder) : base(recorder) { }

    protected override Guid GetCorrelationId(EnrollStudentFromSagaCommand message) => message.CorrelationId;
}

/// <summary>Simulates the Identity role-assignment consumer.</summary>
public class AssignRoleForwarder : ForwarderBase<AssignStudentRoleFromSagaCommand>
{
    public AssignRoleForwarder(ForwarderRecorder recorder) : base(recorder) { }

    protected override Guid GetCorrelationId(AssignStudentRoleFromSagaCommand message) => message.CorrelationId;
}

/// <summary>Simulates RevokeStudentEnrollmentFromSagaConsumer.</summary>
public class RevokeForwarder : ForwarderBase<RevokeStudentEnrollmentFromSagaCommand>
{
    public RevokeForwarder(ForwarderRecorder recorder) : base(recorder) { }

    protected override Guid GetCorrelationId(RevokeStudentEnrollmentFromSagaCommand message) => message.CorrelationId;
}

/// <summary>Simulates the Identity role-removal consumer.</summary>
public class RemoveRoleForwarder : ForwarderBase<RemoveStudentRoleFromSagaCommand>
{
    public RemoveRoleForwarder(ForwarderRecorder recorder) : base(recorder) { }

    protected override Guid GetCorrelationId(RemoveStudentRoleFromSagaCommand message) => message.CorrelationId;
}
