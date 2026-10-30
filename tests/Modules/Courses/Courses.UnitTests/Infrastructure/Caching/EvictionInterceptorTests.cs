using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Data;
using System.Data.Common;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Caching;

/// <summary>
/// Interceptor flush matrix (spec-33 handoff §1.3/§6), driven by REAL saves against a REAL
/// DbContext: a minimal SQLite-backed stub context (the interceptors are context- and
/// provider-agnostic, so no module context is needed; the module model uses Postgres-only column
/// types and cannot build on SQLite).
///
/// Matrix: standalone save success → flush; standalone save failure → discard; transaction
/// (2 saves) + commit → one flush; rollback → discard; TransactionFailed commit → flush;
/// ambient transaction → the guard throws and nothing is flushed; empty queue → no cache calls.
/// </summary>
public class EvictionInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IEvictionProvider _provider;
    private readonly ICachingService _cache;
    private readonly EvictionSaveChangesInterceptor _save;
    private readonly EvictionTransactionInterceptor _tx;
    private StubDbContext _context = null!;

    public EvictionInterceptorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _cache = Substitute.For<ICachingService>();
        _provider = new ScopedEvictionProvider(_cache, Substitute.For<ILogger<ScopedEvictionProvider>>());
        _save = new EvictionSaveChangesInterceptor(_provider);
        _tx = new EvictionTransactionInterceptor(_provider);
    }

    private StubDbContext NewContext(bool withInterceptors = true)
    {
        var builder = new DbContextOptionsBuilder<StubDbContext>().UseSqlite(_connection);
        if (withInterceptors)
            builder.AddInterceptors(_save, _tx);

        var context = new StubDbContext(builder.Options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task StandaloneSave_Success_Flushes()
    {
        _context = NewContext();
        _provider.Begin();
        _provider.Evict(CacheKey.Create("courses:test", "key1"));

        _context.Things.Add(new StubThing());
        await _context.SaveChangesAsync();

        // The standalone save flushed the queued key exactly once.
        await _cache.Received(1).RemoveAsync(
            Arg.Is<CacheKey>(k => k.Value == "courses:test:key1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StandaloneSave_Failure_Discards()
    {
        _context = NewContext();
        _provider.Begin();
        _provider.Evict(CacheKey.Create("courses:test", "key1"));

        // Force a real save failure: the table is gone, so the INSERT commands throw.
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "DROP TABLE Things;";
            command.ExecuteNonQuery();
        }

        _context.Things.Add(new StubThing());
        var act = () => _context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();

        // The failed standalone save burned exactly its queued keys: nothing evicted.
        await _cache.DidNotReceive().RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>());
        await _cache.DidNotReceive().RemoveByTagAsync(Arg.Any<CacheTag>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transaction_TwoSaves_Commit_FlushesExactlyOnce()
    {
        _context = NewContext();
        _provider.Begin();

        await using (var tx = await _context.Database.BeginTransactionAsync())
        {
            _provider.Evict(CacheKey.Create("courses:test", "key1"));

            _context.Things.Add(new StubThing());
            await _context.SaveChangesAsync();
            _context.Things.Add(new StubThing());
            await _context.SaveChangesAsync();

            // Both saves were inside the transaction: nothing flushed yet (the cache stub is untouched).
            await _cache.DidNotReceive().RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>());

            await tx.CommitAsync();
        }

        // The commit flushed each queued key exactly once.
        await _cache.Received(1).RemoveAsync(
            Arg.Is<CacheKey>(k => k.Value == "courses:test:key1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transaction_Rollback_Discards()
    {
        _context = NewContext();
        _provider.Begin();

        await using (var tx = await _context.Database.BeginTransactionAsync())
        {
            _provider.Evict(CacheKey.Create("courses:test", "key1"));
            _provider.EvictTag(CacheTag.Create("courses:test-tag", "views"));

            _context.Things.Add(new StubThing());
            await _context.SaveChangesAsync();

            await tx.RollbackAsync();
        }

        await _cache.DidNotReceive().RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>());
        await _cache.DidNotReceive().RemoveByTagAsync(Arg.Any<CacheTag>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailedSave_InsideATransaction_KeepsTheQueueForTheCommitDecision()
    {
        _context = NewContext();
        _provider.Begin();

        // Force a real save failure inside the transaction.
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "DROP TABLE Things;";
            command.ExecuteNonQuery();
        }

        _provider.Evict(CacheKey.Create("courses:test", "key1"));

        await using (var tx = await _context.Database.BeginTransactionAsync())
        {
            _context.Things.Add(new StubThing());
            await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());

            // The failed save inside a transaction must NOT discard: its keys survive for the commit.
            await _cache.DidNotReceive().RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>());

            await tx.CommitAsync();
        }

        // Commit flushed the keys of the failed save (over-eviction only — never staleness).
        await _cache.Received(1).RemoveAsync(
            Arg.Is<CacheKey>(k => k.Value == "courses:test:key1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransactionFailed_OnCommitPath_FlushesWithNoneToken()
    {
        _context = NewContext();
        _provider.Begin();
        _provider.Evict(CacheKey.Create("courses:test", "key1"));

        // Commit-path failure is ambiguous — the DB may have committed — so the flush is
        // defensive (the queued keys are removed) and non-cancellable.
        await _tx.TransactionFailedAsync(
            new FakeDbTransaction(_connection),
            ErrorEventData(_context, new FakeDbTransaction(_connection), "Commit"),
            CancellationToken.None);

        await _cache.Received(1).RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransactionFailed_OnRollbackPath_Discards()
    {
        _context = NewContext();
        _provider.Begin();
        _provider.Evict(CacheKey.Create("courses:test", "key1"));

        await _tx.TransactionFailedAsync(
            new FakeDbTransaction(_connection),
            ErrorEventData(_context, new FakeDbTransaction(_connection), "Rollback"),
            CancellationToken.None);

        await _cache.DidNotReceive().RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>());
    }

    private static TransactionEndEventData CommittedEventData(DbContext context, DbTransaction transaction) =>
        new(null!, null!, transaction, context, transactionId: Guid.NewGuid(), connectionId: Guid.NewGuid(),
            async: true, startTime: DateTimeOffset.UtcNow, duration: TimeSpan.Zero);

    private static TransactionErrorEventData ErrorEventData(
        DbContext context, DbTransaction transaction, string action) =>
        new(null!, null!, transaction, context, transactionId: Guid.NewGuid(), connectionId: Guid.NewGuid(),
            async: true, action, new InvalidOperationException($"{action} failed"),
            startTime: DateTimeOffset.UtcNow, duration: TimeSpan.Zero);

    private sealed class FakeDbTransaction(DbConnection connection) : DbTransaction
    {
        // .NET 10: Connection is now non-virtual; the abstract member is the protected DbConnection.
        protected override DbConnection DbConnection { get; } = connection;
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public override void Commit() { }
        public override void Rollback() { }
        protected override void Dispose(bool disposing) { }
    }

    [Fact]
    public async Task AmbientTransaction_TheGuardThrows_AndNothingIsFlushed()
    {
        _context = NewContext();
        _provider.Begin();

        using var scope = new System.Transactions.TransactionScope(
            System.Transactions.TransactionScopeAsyncFlowOption.Enabled);
        System.Transactions.Transaction.Current.Should().NotBeNull();

        var act = () => _context.SaveChangesAsync();

        // The ambient guard fails fast — an ambient commit has no EF hook to flush at.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ambient*");
        await _cache.DidNotReceive().RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>());
        await _cache.DidNotReceive().RemoveByTagAsync(Arg.Any<CacheTag>(), Arg.Any<CancellationToken>());
        scope.Complete();
    }

    [Fact]
    public async Task StandaloneSave_WithNothingQueued_FlushIsANoOpForTheCache()
    {
        _context = NewContext();

        _context.Things.Add(new StubThing());
        await _context.SaveChangesAsync();

        // The flush ran (the interceptor calls it unconditionally on a standalone save), but the
        // empty queue means the cache was never touched.
        await _cache.DidNotReceive().RemoveAsync(Arg.Any<CacheKey>(), Arg.Any<CancellationToken>());
        await _cache.DidNotReceive().RemoveByTagAsync(Arg.Any<CacheTag>(), Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _context?.Dispose();
        _connection.Dispose();
    }

    private sealed class StubDbContext(DbContextOptions<StubDbContext> options) : DbContext(options)
    {
        public DbSet<StubThing> Things => Set<StubThing>();
    }

    private sealed class StubThing
    {
        public int Id { get; set; }
        public string Name { get; set; } = "stub";
    }
}
