using AlphaZero.Shared.Infrastructure.Caching;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Caching;

public class EvictionPipelineTests
{
    private static readonly CacheTag Tag = CacheTag.Create("courses:course", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task NestedDispatch_DoesNotFlushUntilOutermostCompletes()
    {
        var provider = Substitute.For<IEvictionProvider>();
        var scope = new EvictionFlushScope();
        var outer = new CacheEvictionFlushBehavior<OuterRequest, ErrorOr<Success>>(provider, scope);
        var inner = new CacheEvictionFlushBehavior<InnerRequest, ErrorOr<Success>>(provider, scope);

        var result = await outer.Handle(new OuterRequest(), async _ =>
        {
            var innerResult = await inner.Handle(
                new InnerRequest(),
                _ => Task.FromResult<ErrorOr<Success>>(Result.Success),
                CancellationToken.None);

            innerResult.IsError.Should().BeFalse();
            // The nested dispatch must not touch the cache: the outer UnitOfWork has not committed.
            await provider.DidNotReceive().FlushAsync(Arg.Any<CancellationToken>());
            provider.DidNotReceive().Discard();
            return Result.Success;
        }, CancellationToken.None);

        result.IsError.Should().BeFalse();
        provider.Received(1).Begin(); // outermost only
        await provider.Received(1).FlushAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ErrorResult_DiscardsQueuedTags_AndNeverFlushes()
    {
        var provider = Substitute.For<IEvictionProvider>();
        var behavior = new CacheEvictionFlushBehavior<OuterRequest, ErrorOr<Success>>(
            provider, new EvictionFlushScope());

        var result = await behavior.Handle(
            new OuterRequest(),
            _ => Task.FromResult<ErrorOr<Success>>(Error.Failure("Test.Failure", "failed")),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        provider.Received(1).Begin();
        provider.Received(1).Discard();
        await provider.DidNotReceive().FlushAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exception_DiscardsQueuedTags_AndPropagates()
    {
        var provider = Substitute.For<IEvictionProvider>();
        var behavior = new CacheEvictionFlushBehavior<OuterRequest, ErrorOr<Success>>(
            provider, new EvictionFlushScope());

        var act = () => behavior.Handle(
            new OuterRequest(),
            _ => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        provider.Received(1).Begin();
        provider.Received(1).Discard();
        await provider.DidNotReceive().FlushAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FlushAsync_EvictsEachQueuedTagOnce_AndClearsTheQueue()
    {
        var cache = Substitute.For<ICachingService>();
        var provider = new ScopedEvictionProvider(cache, Substitute.For<ILogger<ScopedEvictionProvider>>());

        provider.Begin();
        provider.Evict(Tag);
        provider.Evict([Tag, CacheTag.Create("courses:catalog", Guid.NewGuid().ToString("N"))]);

        await provider.FlushAsync(CancellationToken.None);
        await provider.FlushAsync(CancellationToken.None); // queue is already empty

        await cache.Received(1).RemoveByTagAsync(Tag, Arg.Any<CancellationToken>());
        await cache.Received(2).RemoveByTagAsync(Arg.Any<CacheTag>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FlushAsync_WhenCacheFails_LogsAndSwallows_BecauseTheWriteAlreadyCommitted()
    {
        var cache = Substitute.For<ICachingService>();
        cache.RemoveByTagAsync(Arg.Any<CacheTag>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("cache down")));
        var logger = Substitute.For<ILogger<ScopedEvictionProvider>>();
        var provider = new ScopedEvictionProvider(cache, logger);

        provider.Begin();
        provider.Evict(Tag);

        var act = () => provider.FlushAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        await cache.Received(1).RemoveByTagAsync(Tag, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FlushAsync_WhenCacheCancels_PropagatesCancellation()
    {
        var cache = Substitute.For<ICachingService>();
        cache.RemoveByTagAsync(Arg.Any<CacheTag>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException()));
        var provider = new ScopedEvictionProvider(cache, Substitute.For<ILogger<ScopedEvictionProvider>>());

        provider.Begin();
        provider.Evict(Tag);

        var act = () => provider.FlushAsync(CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Discard_DropsQueuedTags_WithoutEvicting()
    {
        var cache = Substitute.For<ICachingService>();
        var provider = new ScopedEvictionProvider(cache, Substitute.For<ILogger<ScopedEvictionProvider>>());

        provider.Begin();
        provider.Evict(Tag);
        provider.Discard();

        await provider.FlushAsync(CancellationToken.None);

        await cache.DidNotReceive().RemoveByTagAsync(Arg.Any<CacheTag>(), Arg.Any<CancellationToken>());
    }

    private sealed record OuterRequest;
    private sealed record InnerRequest;
}
