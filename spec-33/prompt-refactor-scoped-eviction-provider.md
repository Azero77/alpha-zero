# Task: refactor `ScopedEvictionProvider.cs` to remove the explicit locks

## Repo / branch context

You are on branch `spec/33-caching` (worktree path in session). One source file is the target:

- `src/alphazero-api/AlphaZero.Shared/Infrastructure/Caching/ScopedEvictionProvider.cs`

Read it first. It implements `IEvictionProvider` (same folder) — a request-scoped dual
eviction queue used by the Courses module's repository decorators to queue cache keys/tags,
and by two EF Core interceptors in `src/alphazero-api/AlphaZero.Shared/Infrastructure/UnitOfWork.cs`
that call `FlushAsync`/`Discard` at DB save/commit points. Callers of the interface:
the four `Caching*RepositoryDecorator` classes in
`src/alphazero-api/Modules/Courses/Infrastructure/Caching/`, the two interceptors, and
`tests` suites listed below.
Supporting types: `CacheKey`, `CacheTag` (same folder), `ICachingService`.

## The current synchronization the owner dislikes

A single `private readonly object _gate` serializes `IsActive`, `HasPendingKeys`, `Begin`,
`Evict`, `EvictTag`, `Discard`, and the snapshot-and-clear inside `FlushAsync`. The owner wants
this lock-heavy style gone. **Target: zero explicit `lock` statements in this file.**
If (and only if) you conclude that is impossible without losing a required semantic, you may keep
ONE minimal lock, but you must write a justification in your report explaining why no lock-free
design satisfies the invariants.

## Hard invariants (these are binding — breaking any one is a failed refactor)

1. **Queue ownership must stay request-scoped instance state — it must NOT become `AsyncLocal`
   or any flow-scoped mechanism.** This is deliberate and load-bearing: queuing happens in
   command handlers, which are DESCENDANT async contexts of the pipeline that later calls
   `SaveChangesAsync` and flushes; `AsyncLocal` writes in a descendant never flow back up to an
   ancestor, which made an earlier AsyncLocal design provably broken (every flush saw empty
   lanes). Everything that participates in one write (decorators, unit of work, DbContext, both
   interceptors) resolves this SAME scoped instance from one DI child scope.
2. **Flush semantics = snapshot-and-clear, atomic.** `FlushAsync` must observe a point-in-time
   set of the pending keys/tags, clear them in place, and evict exactly that snapshot. A queuer
   that lands while/after the flush is taking its snapshot belongs to the NEXT save — it must
   never be lost and must never be evicted twice by an enclosing retry.
3. **Dedup semantics.** Queuing the same key or tag twice must result in exactly one removal
   per flush point (current `HashSet` lanes give this; tests assert it).
4. **Cache I/O must never happen inside any synchronization region.** After your refactor, the
   same rule holds structurally: no detached queued work, no fire-and-forget, no `lock` over an
   `await`. The cache awaits stay where they are (inside `FlushAsync`, after the snapshot).
5. **Failure/perf semantics are exactly:** `OperationCanceledException` from cache calls
   propagates; every other exception is logged (same message shape) and swallowed — the write is
   already committed, eviction failure only delays freshness. An empty snapshot is a no-op that
   makes zero cache calls. Keys are flushed before tags (keep the order).
6. **Public API unchanged:** `IEvictionProvider` members, signatures, XML doc semantics, the
   class being `sealed`, DI registration (scoped) — all stay as they are.
7. **No new features:** no staging states, no retry logic, no persistence, no MediatR-coupling,
   no new dependencies on domain or module code.

## Suggested designs (you choose and justify — they are candidates, not orders)

- **A) Immutable lanes + atomic swap:** hold an immutable "lanes" state (e.g. a private sealed
  record with two readonly HashSets, or `ImmutableHashSet` — check whether
  `System.Collections.Immutable` is already referenced in the shared project before adding a
  package; do NOT add packages) in a `volatile` field. `Evict` = copy-on-write add; `FlushAsync`
  snapshot = `Interlocked.Exchange` the field to the empty state. This makes snapshot-and-clear
  a single atomic operation with no lock, and queuers-after-snapshot naturally mutate the new
  empty state (invariant 2 falls out).
- **B) Concurrent drain:** per-lane `ConcurrentDictionary<CacheKey, byte>` /
  `ConcurrentDictionary<CacheTag, byte>` for dedupe, and a flush that atomically drains: exchange
  the container with a fresh empty one, then enumerate the drained dictionary. Watch for keys
  added between "drained reference captured" and "drain completes" (design so none are lost).
- Provide your chosen design's thread-safety argument in the class XML doc: which operation is
  atomic, why nothing is lost, why dedupe holds, why a queuer racing the flush is safe (either
  coalesced into the flush or carried to the next one — pick and state which).

## Tests — they must keep passing, unmodified

- `tests/Modules/Courses/Courses.UnitTests/Infrastructure/Caching/ScopedEvictionProviderTests.cs`
  (dual lanes, dedupe, snapshot-and-clear, discard, failure-swallow, cancellation, empty no-op).
- `tests/Modules/Courses/Courses.UnitTests/Infrastructure/Caching/EvictionInterceptorTests.cs`
  (flush/discard semantics at the interceptor level).
- Full unit suite must be green:
  `dotnet test tests/Modules/Courses/Courses.UnitTests/Courses.UnitTests.csproj --nologo -v q`
  (baseline: 239 passed).
- `dotnet build AlphaZero.sln --nologo -v q` — the ONLY allowed build errors are five
  pre-existing ones in
  `tests/Modules/VideoUploading/VideoUploading.Tests.Integration/CreatePlaybackSessionHandlerUnitTests.cs`
  (CS1503 ×4 at lines 91/117/150/185, CS1061 at 212). Never touch that file.
- Do NOT run the integration suite (slow, Testcontainers).

**Add one new test** (may live in the existing provider tests file): a contention test that from
several concurrent tasks queues a known set of keys while another task runs `FlushAsync` in a
loop; after quiescence, one more `FlushAsync` + assert that the union of flushed removals equals
the queued set with no duplicates and nothing lost. If your design's flush/queue race means a key
could legitimately be evicted by EITHER the running flush or the next one, the test should assert
"exactly one removal per queued key across all flushes".

## Deliverables

1. The refactored `ScopedEvictionProvider.cs` (only this file; do not touch
   `IEvictionProvider.cs`, decorators, interceptors, or shared DI).
2. Updated class XML documentation describing the sync protocol (in plain prose — e.g. write
   "change tracker" style wording carefully; avoid claiming thread-safety you haven't argued).
3. The new contention test.
4. A short report: chosen design + why, the thread-safety argument, `dotnet build` and unit-test
   output, and any deviation from the suggested designs with justification.
5. Commit on `spec/33-caching` referencing `#33`, message prefixed `refactor(shared):`.
