using AlphaZero.Shared.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Caching;

/// <summary>
/// Two-lane queue contract (keys + fan-out tag): dedupe, snapshot-and-clear flush, cancellation
/// and failure semantics, discard, and empty-queue no-op (§1.4 of the spec-33 handoff).
/// </summary>
public class ScopedEvictionProviderTests
{
    private static CacheKey Key(string name) => CacheKey.Create("courses:test", name);
    private static CacheTag Tag(string name) => CacheTag.Create("courses:test-tag", name);

    private static (ScopedEvictionProvider Provider, ICachingService Cache) Create(
        ILogger<ScopedEvictionProvider>? logger = null)
    {
        var cache = Substitute.For<ICachingService>();
        return (new ScopedEvictionProvider(cache, logger ?? Substitute.For<ILogger<ScopedEvictionProvider>>()), cache);
    }

    [Fact]
    public void Begin_InitializesBothLanes()
    {
        var (provider, _) = Create();

        provider.IsActive.Should().BeFalse();
        provider.HasPendingKeys.Should().BeFalse();

        provider.Begin();

        provider.IsActive.Should().BeTrue();
        provider.HasPendingKeys.Should().BeFalse();
    }

    [Fact]
    public async Task FlushAsync_RemovesEachUniqueKey_AndEachUniqueTag_ThenClearsBothLanes()
    {
        var (provider, cache) = Create();
        var keyA = Key("a");
        var tagA = Tag("a");

        provider.Begin();
        provider.Evict(keyA);
        provider.Evict(keyA); // dedupe
        provider.Evict([Key("b"), keyA]);
        provider.EvictTag(tagA);
        provider.EvictTag(tagA);
        provider.HasPendingKeys.Should().BeTrue();

        await provider.FlushAsync(CancellationToken.None);
        await provider.FlushAsync(CancellationToken.None); // lanes already empty → no-op

        await cache.Received(1).RemoveAsync(keyA, Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveAsync(Key("b"), Arg.Any<CancellationToken>());
        await cache.Received(2).RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveByTagAsync(tagA, Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveByTagAsync(Arg.Any<CacheTag>(), Arg.Any<CancellationToken>());
        provider.HasPendingKeys.Should().BeFalse();
    }

    [Fact]
    public void Evict_OutsideABegin_StillQueuesTheKey()
    {
        var (provider, cache) = Create();

        provider.Evict(Key("orphan"));

        provider.HasPendingKeys.Should().BeTrue();
    }

    [Fact]
    public async Task FlushAsync_WhenKeyRemovalFails_LogsAndSwallows_BecauseTheWriteAlreadyCommitted()
    {
        var cache = Substitute.For<ICachingService>();
        cache.When(c => c.RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>()))
            .Throw(new InvalidOperationException("cache down"));
        var logger = new RecordingLogger<ScopedEvictionProvider>();
        var provider = new ScopedEvictionProvider(cache, logger);

        provider.Evict(Key("a"));
        provider.EvictTag(Tag("t"));

        var act = () => provider.FlushAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        logger.Entries.Should().ContainSingle()
            .Which.Level.Should().Be(LogLevel.Warning);
        var entry = logger.Entries.Single();
        entry.Message.Should().Contain("Cache eviction failed");
        entry.Exception.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("cache down");
    }

    [Fact]
    public async Task FlushAsync_WhenTagRemovalFails_LogsAndSwallows_AndStillFlushesRemainingTags()
    {
        var cache = Substitute.For<ICachingService>();
        var failing = Tag("fail");
        cache.RemoveByTagAsync(failing, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("cache down")));
        var logger = new RecordingLogger<ScopedEvictionProvider>();
        var provider = new ScopedEvictionProvider(cache, logger);

        provider.EvictTag(failing);
        provider.EvictTag(Tag("ok"));

        var act = () => provider.FlushAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        await cache.Received(1).RemoveByTagAsync(Tag("ok"), Arg.Any<CancellationToken>());
        logger.Entries.Should().ContainSingle()
            .Which.Message.Should().Contain("Cache eviction failed");
    }

    [Fact]
    public async Task FlushAsync_WhenCacheCancels_PropagatesOperationCanceledException()
    {
        var cache = Substitute.For<ICachingService>();
        cache.RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException()));
        var provider = new ScopedEvictionProvider(cache, Substitute.For<ILogger<ScopedEvictionProvider>>());

        provider.Evict(Key("a"));

        var act = () => provider.FlushAsync(CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FailedFlush_IsNotRetriedByAnEnclosingScope()
    {
        var cache = Substitute.For<ICachingService>();
        cache.RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException()));
        var provider = new ScopedEvictionProvider(cache, Substitute.For<ILogger<ScopedEvictionProvider>>());

        provider.Evict(Key("a"));
        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.FlushAsync(CancellationToken.None));

        // The snapshot-and-clear happened before the failure: a second flush finds an empty queue.
        provider.HasPendingKeys.Should().BeFalse();
        await provider.FlushAsync(CancellationToken.None);
        await cache.Received(1).RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Discard_ClearsBothLanes_WithoutEvicting()
    {
        var (provider, cache) = Create();

        provider.Begin();
        provider.Evict(Key("a"));
        provider.EvictTag(Tag("t"));
        provider.Discard();
        await provider.FlushAsync(CancellationToken.None);

        await cache.DidNotReceive().RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>());
        await cache.DidNotReceive().RemoveByTagAsync(Arg.Any<CacheTag>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmptyQueue_FlushAndDiscard_AreNoOps()
    {
        var (provider, cache) = Create();

        provider.Begin();
        await provider.FlushAsync(CancellationToken.None);
        provider.Discard();

        await cache.DidNotReceive().RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>());
        await cache.DidNotReceive().RemoveByTagAsync(Arg.Any<CacheTag>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConcurrentProducersAndFlush_NeverLoseAKey()
    {
        var cache = Substitute.For<ICachingService>();
        var removedKeys = new System.Collections.Concurrent.ConcurrentBag<CacheKey>();
        cache.RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => removedKeys.Add(ci.Arg<CacheKey>()));

        var provider = new ScopedEvictionProvider(cache, Substitute.For<ILogger<ScopedEvictionProvider>>());
        provider.Begin();

        const int producerCount = 8;
        const int keysPerProducer = 50;
        var allKeys = Enumerable.Range(0, producerCount * keysPerProducer)
            .Select(i => Key($"contention-{i}"))
            .ToArray();

        using var barrier = new Barrier(producerCount + 1);

        var producers = Enumerable.Range(0, producerCount).Select(p => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = p * keysPerProducer; i < (p + 1) * keysPerProducer; i++)
            {
                provider.Evict(allKeys[i]);
            }
        })).ToArray();

        var flusher = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            while (!Task.WhenAll(producers).IsCompleted)
            {
                await provider.FlushAsync(CancellationToken.None);
                await Task.Yield();
            }
            // Final drain after producers quiesce
            await provider.FlushAsync(CancellationToken.None);
        });

        await Task.WhenAll(producers);
        await flusher;

        removedKeys.Should().HaveCount(allKeys.Length,
            "every queued key must be removed exactly once across all flushes — no doubles");
        removedKeys.ToHashSet().Should().BeEquivalentTo(allKeys,
            "every queued key must be removed across all flushes — no loss");
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
