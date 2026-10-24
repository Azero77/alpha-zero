using System.Collections;
using AlphaZero.Shared.Infrastructure.Messaging;
using AlphaZero.Shared.Infrastructure.Tenats;
using MassTransit;
using NSubstitute;

namespace Tenants.UnitTests.Infrastructure.Messaging;

public class TenantConsumeFilterTests
{
    private readonly IAmbientTenantContext _ambient = new AmbientTenantContext();

    [Fact]
    public async Task Send_PrefersMessageTenantId_OverEnvelopeHeader()
    {
        var messageTenantId = Guid.NewGuid();
        var headerTenantId = Guid.NewGuid();
        var context = ContextFor(new TenantMessage(messageTenantId), headerTenantId);
        Guid? observed = null;
        var next = PipeFor<TenantMessage>(c => observed = _ambient.TenantId);

        await new TenantConsumeFilter<TenantMessage>(_ambient).Send(context, next);

        observed.Should().Be(messageTenantId);
    }

    [Fact]
    public async Task Send_FallsBackToEnvelopeHeader_WhenMessageHasNoTenant()
    {
        var headerTenantId = Guid.NewGuid();
        var context = ContextFor(new TenantlessMessage(), headerTenantId);
        Guid? observed = null;
        var next = PipeFor<TenantlessMessage>(c => observed = _ambient.TenantId);

        await new TenantConsumeFilter<TenantlessMessage>(_ambient).Send(context, next);

        observed.Should().Be(headerTenantId);
    }

    [Fact]
    public async Task Send_LeavesAmbientEmpty_WhenNoTenantIsAvailable()
    {
        var context = ContextFor(new TenantlessMessage(), headerTenantId: null);
        Guid? observed = Guid.NewGuid();
        var next = PipeFor<TenantlessMessage>(c => observed = _ambient.TenantId);

        await new TenantConsumeFilter<TenantlessMessage>(_ambient).Send(context, next);

        observed.Should().BeNull();
    }

    [Fact]
    public async Task Send_RestoresPreviousAmbientTenant_AfterDispatch()
    {
        var outerTenantId = Guid.NewGuid();
        var innerTenantId = Guid.NewGuid();
        var context = ContextFor(new TenantMessage(innerTenantId), headerTenantId: null);
        var next = PipeFor<TenantMessage>(_ => { });

        using (_ambient.Push(outerTenantId))
        {
            await new TenantConsumeFilter<TenantMessage>(_ambient).Send(context, next);
            _ambient.TenantId.Should().Be(outerTenantId);
        }

        _ambient.TenantId.Should().BeNull();
    }

    private static ConsumeContext<T> ContextFor<T>(T message, Guid? headerTenantId)
        where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        context.Headers.Returns(new FakeHeaders(
            headerTenantId is null
                ? null
                : new Dictionary<string, object> { [TenantConsumeFilter<T>.HeaderName] = headerTenantId.Value.ToString() }));
        return context;
    }

    private static IPipe<ConsumeContext<T>> PipeFor<T>(Action<ConsumeContext<T>> onSend)
        where T : class
        => new Pipe<ConsumeContext<T>>(context =>
        {
            onSend(context);
            return Task.CompletedTask;
        });

    public sealed record TenantMessage(Guid TenantId);

    public sealed record TenantlessMessage;

    private sealed class Pipe<T> : IPipe<T>
        where T : class, PipeContext
    {
        private readonly Func<T, Task> _onSend;

        public Pipe(Func<T, Task> onSend) => _onSend = onSend;

        public Task Send(T context) => _onSend(context);

        public void Probe(ProbeContext context) => context.CreateFilterScope("test-pipe");
    }

    private sealed class FakeHeaders : Headers
    {
        private readonly Dictionary<string, object> _values;

        public FakeHeaders(Dictionary<string, object>? values = null)
            => _values = values ?? new Dictionary<string, object>();

        public IEnumerable<KeyValuePair<string, object>> GetAll() => _values;

        public bool TryGetHeader(string key, out object value) => _values.TryGetValue(key, out value!);

        public T? Get<T>(string key, T? defaultValue = null) where T : class
            => _values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;

        public T? Get<T>(string key, T? defaultValue) where T : struct
            => _values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;

        public IEnumerator<HeaderValue> GetEnumerator()
            => _values.Select(pair => new HeaderValue(pair.Key, pair.Value)).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
