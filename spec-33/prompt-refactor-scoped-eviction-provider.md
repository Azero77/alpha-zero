# Task prompt: refactor `ScopedEvictionProvider.cs` — remove the explicit locks

You are a fresh AI on an existing .NET 10 codebase. Your task is a **single-file refactor** with a
full test gate. Read prompt sections 2–5 completely before writing any code; invariants in §4 are
binding, and six alternative designs in §5 are the only accepted candidates.

Worktree / branch: `spec/33-caching` (paths below are relative to the repo root).
All work in one commit on this branch, message prefixed `refactor(shared):`, ending with `(#33)`.

---

## 1. One-paragraph background

`ScopedEvictionProvider` is a request-scoped queue of cache evictions for the Courses module.
Write-side repository decorators queue exact `CacheKey`s (and one rare `CacheTag`) when a handler
calls a decorated write; **two EF Core interceptors in `UnitOfWork.cs` call `FlushAsync` at DB
save/commit points** and `Discard` on failure/rollback paths. The current implementation guards
its two `HashSet` lanes with a single `private readonly object _gate` lock. The owner dislikes
the lock-heavy style and wants a refactor that removes the locks **without changing any
observable behavior**.

---

## 2. Referenced files (read all of these; the roles explain why each matters)

| File | Role | What to look at |
|---|---|---|
| `src/alphazero-api/AlphaZero.Shared/Infrastructure/Caching/ScopedEvictionProvider.cs` | **THE TARGET FILE** (~155 lines) | the `_gate` lock and its 6 synchronized members; `FlushAsync` snapshot-and-clear at :90–103; failure handling :108–144 |
| `src/alphazero-api/AlphaZero.Shared/Infrastructure/Caching/IEvictionProvider.cs` | the interface it implements | public contract — **must not change** (members, signatures, doc meaning) |
| `src/alphazero-api/AlphaZero.Shared/Infrastructure/Caching/CacheKey.cs` | `CacheKey`/`CacheTag` value types (readonly record structs with `Value`) | they are the lane element types; thread-safe to pass around |
| `src/alphazero-api/AlphaZero.Shared/Infrastructure/Caching/ICachingService.cs` | cache port used inside `FlushAsync` | `RemoveAsync(CacheKey, ct)` :12, `RemoveByTagAsync(CacheTag, ct)` :21 |
| `src/alphazero-api/AlphaZero.Shared/Infrastructure/Caching/HybridCachingService.cs` | HybridCache adapter | supplies the cache I/O awaited in `FlushAsync`; constructor resolves the eviction provider lazily via `IServiceProvider` (:78–83 use the provider) — do not disturb that di-cycle fix |
| `src/alphazero-api/AlphaZero.Shared/Infrastructure/UnitOfWork.cs` | **flush actor** | `EvictionSaveChangesInterceptor` + `EvictionTransactionInterceptor` call `FlushAsync`/`Discard`/`Begin`; read them to understand when and on which thread your methods run (inside the EF save pipeline, AFTER the handler's awaits — i.e. on the same async flow, but from an ANCESTOR of the code that queued) |
| `src/alphazero-api/Modules/Courses/Infrastructure/Caching/CachingCourseRepositoryDecorator.cs` | queue producer #1 | `Evict(...)` calls per write; note `TenantId == Guid.Empty` → queues nothing |
| `src/alphazero-api/Modules/Courses/Infrastructure/Caching/CachingCourseOverviewRepositoryDecorator.cs` | queue producer #2 | same shape |
| `src/alphazero-api/Modules/Courses/Infrastructure/Caching/CachingCourseAssetRepositoryDecorator.cs` | queue producer #3 | same shape |
| `src/alphazero-api/Modules/Courses/Infrastructure/Caching/CachingSubjectRepositoryDecorator.cs` | queue producer #4 | queues a key set **and** the fan-out tag via `EvictTag` (:76) — the only tag producer |
| `src/alphazero-api/AlphaZero.Shared/Infrastructure/DependencyInjection.cs` | DI wiring | scoped `ScopedEvictionProvider` + both interceptors registered around :57–62 — lifetime must stay scoped, do not change registration style |
| `tests/Modules/Courses/Courses.UnitTests/Infrastructure/Caching/ScopedEvictionProviderTests.cs` | **behavior spec (tests)** | dual lanes, dedupe, snapshot-and-clear, discard, failure-swallow, cancellation, empty no-op — must pass UNMODIFIED |
| `tests/Modules/Courses/Courses.UnitTests/Infrastructure/Caching/EvictionInterceptorTests.cs` | **behavior spec (tests)** | flush-on-commit / discard-on-rollback matrix via a SQLite stub context — must pass UNMODIFIED |
| `tests/Modules/Courses/Courses.UnitTests/Infrastructure/Caching/CoursesRepositoryDecoratorTests.cs` | consumer-side tests | exact queued key sets asserted via the decorators; unchanged |
| `tests/Modules/Courses/Courses.UnitTests/Infrastructure/Caching/SharedCachingRegistrationTests.cs` | composition test | unchanged |
| `tests/Modules/VideoUploading/VideoUploading.Tests.Integration/CreatePlaybackSessionHandlerUnitTests.cs` | **DO NOT TOUCH** | 5 pre-existing build errors (CS1503 ×4 at :91/:117/:150/:185, CS1061 at :212) are owner-accepted baseline noise |
| `spec-33/spec-33-caching-handoff.md` | binding design contract | §1.4 (scoped-lane rationale), §9 (hard boundaries), §10 (verification) — the invariants in §4 below come from there |
| `spec-33/code-review-plan.md` | design record | §4.3 (provider contract as built), §4.8 (failure table) |

Skim `docs/caching/README.md` §2.5–§3 only if you need more pipeline background (not required).

---

## 3. Current implementation summary (what you are replacing)

- Two `HashSet` lanes (`_pendingKeys`, `_pendingTags`) + one `object _gate`.
- `IsActive` / `HasPendingKeys` — read under lock (consulted by the interceptors' guards).
- `Begin` — lazily initializes both lanes (called by the save interceptor at flow start).
- `Evict(key)` / `Evict(keys)` / `EvictTag(tag)` — add under lock (a producer may legally run
  concurrently with other producers, e.g. parallel MediatR publish strategies).
- `FlushAsync(ct)` — **snapshot-and-clear both lanes in one critical section** (:90–103), then
  awaits `RemoveAsync` per unique key, then `RemoveByTagAsync` per unique tag, with cache I/O
  OUTSIDE the lock; empty snapshot → zero cache calls; cancellation propagates, other
  exceptions logged-and-swallowed.
- `Discard` — clear both lanes under lock.

---

## 4. Binding invariants (breaking any one = failed refactor)

1. **Queue ownership stays request-scoped instance state.** It must NOT become `AsyncLocal` or
   any flow-execution-context mechanism. Reason: queuing happens in command handlers, which are
   DESCENDANT async contexts of the pipeline that later calls `SaveChangesAsync`; AsyncLocal
   writes in a descendant never flow back up to the ancestor, which made an earlier AsyncLocal
   design provably broken (every flush observed empty lanes). All participants resolve the SAME
   scoped instance from one DI child scope.
2. **`FlushAsync` = atomic snapshot-and-clear.** It must observe a point-in-time set, clear it in
   place, and evict exactly that snapshot. A producer landing during/after the snapshot belongs
   to the NEXT save — it must be either coalesced into THIS snapshot (both are semantically
   safe) or preserved for the next flush — but it must **never be lost** and, across any pattern
   of concurrent saves, each queued key/tag leads to **exactly one** cache removal.
3. **Dedup.** The same key/tag queued twice results in one removal per flush point (current
   `HashSet` semantics; tests assert it).
4. **No synchronization region spans an `await`.** No lock/LockSlim over the cache awaits. After
   the lock-free refactor this must hold structurally (nothing blocks an awaiting thread).
5. **Failure semantics verbatim.** `OperationCanceledException` from cache calls propagates;
   every other exception is logged with the same message shape and swallowed (the write is
   already committed; eviction failure only delays freshness — entries die by TTL).
6. **Empty snapshot = no-op**, zero cache calls, and keys are removed before tags (keep the
   current deterministic order — tests may rely on it).
7. **Public surface unchanged:** `IEvictionProvider` members/signatures/doc meaning, `sealed`
   class, sizes and lifetimes of DI registrations, constructor parameters
   (`ICachingService`, `ILogger<ScopedEvictionProvider>`).

Also forbidden (spec-33 handoff §9 hard boundaries): any EF `ChangeTracker` read, any database
lookup, any staging/pending-confirmation state, any raw key/tag string, any new package
dependency unless it is already referenced by the project (check the `.csproj` first).

---

## 5. Candidate designs — ALL accepted variants

Pick **exactly one**. For the chosen one, document the sync protocol in the class XML doc
(plain prose): which operation is atomic, why nothing is lost, why dedupe holds, which way a
producer racing a flush lands (coalesced into the snapshot vs carried to the next — state which).
The others are rejected designs you must NOT half-implement.

**A. Immutable lanes + atomic swap (RECOMMENDED)**
`volatile` field holds an immutable state: a private `sealed record Lanes(HashSet<CacheKey>
Keys, HashSet<CacheTag> Tags)` starting empty (or two `ImmutableHashSet` fields if
`System.Collections.Immutable` is already referenced — verify first; **do not add packages**).
`Evict(key)` = copy-on-write: replace the whole state with `state.Keys.Add(key)` via a
compare-and-swap retry loop (or just an assignment if you accept rare lost-update-safe last-write
— NO: you must not lose adds; use CAS or keep A simple by noting producers-are-rare and still CAS).
`FlushAsync` snapshot = `Interlocked.Exchange(ref _state, Lanes.Empty)` — atomic snapshot-and-clear
in one operation. `Begin`/`Discard` = trivial store. Queuers-after-snapshot naturally mutate the
fresh empty state (invariant 2 falls out structurally).
Trade-off: per-add cost O(lanes) copy — negligible for a request-scoped queue of ≤ a few dozen
entries; zero locks; easiest to prove.

**B. Concurrent containers + drain semantics**
Per lane: `ConcurrentDictionary<CacheKey, byte>` (dedupe for free), and `FlushAsync` atomically
swaps the container with a fresh empty one (`Interlocked.Exchange`), then drains the captured
instance. Gotcha to design carefully: keys added to the DRAINED instance *after* the swap (safe —
belong to the next save) vs added before the swap but not yet visible in the captured reference
(must be impossible — show why). No lock anywhere. Slightly more allocation churn per request.

**C. `ImmutableHashSet` the whole way (A's BCL dependency variant)**
Same as A using `ImmutableHashSet<CacheKey>`/`<CacheTag>` with CAS on two volatile references
(one per lane) — the flush snapshot must still capture BOTH lanes atomically: do two separate
exchanges and you have a skew window (a saved key flushable twice, or a tag flushed without its
keys — assess whether the failure table tolerates this; the safest C-flavor is one record holding
both sets, i.e. really A). Include only if `System.Collections.Immutable` is already referenced.

**D. Actor/queue-drain with a `Channel<T>`**
All mutations funneled into a `Channel<CacheKey/CacheTag>` consumed asynchronously. REJECTED by
default: it creates an async pump whose lifecycle must be tied to the request scope (unobserved
consumers = staging states — forbidden by invariant 7), and drain-atomicity at flush time is not
guaranteeable without extra state. Only acceptable if you can hold ALL semantics with zero
leftover state at scope end AND keep a deterministic keys-before-tags order — expected to fail;
do not choose unless you can prove otherwise.

**E. `ReaderWriterLockSlim` (multiple-reader single-writer lanes)**
Still a lock — choose ONLY under the escape hatch (worst case if you can prove every lock-free
variant violates an invariant). Critical sections must exclude all cache awaits (invariant 4).

**F. Minimal-lock fallback ("one tiny master")**
Fold both lanes into ONE atomic unit behind a single `lock` but shrink the footprint: a private
nested `sealed class Lane<T>` with the synchronized add/clear inside, the public methods
thin, and a full written argument why no variant in A–D preserves the semantics. This is the
explicitly allowed fallback from §1 of your report — first try A/B.

Concurrency choice you may optionally combine with A/B: producers are few (#4 decorators times
section/reorder handlers), flushes are per save/commit. State your reasoning; do not tune for
fake throughput.

---

## 6. Test gate (all must be true before you report done)

1. Run:
   `dotnet test tests/Modules/Courses/Courses.UnitTests/Courses.UnitTests.csproj --nologo -v q`
   → must show **239 passed, 0 failed** (baseline).
2. The two spec test files
   (`ScopedEvictionProviderTests.cs`, `EvictionInterceptorTests.cs`) contain **zero diffs** from
   `git diff` — they are the behavioral contract.
3. Add exactly ONE new contention test (may live in `ScopedEvictionProviderTests.cs`): several
   concurrent tasks queue a known key set while another task runs `FlushAsync` in a loop; after
   quiescence, assert across ALL flushes (count the cache-removal calls): every queued key removed
   exactly once (no loss, no doubles), under your design's stated race rule.
4. `dotnet build AlphaZero.sln --nologo -v q` from the repo root → the ONLY errors allowed are the
   five pre-existing VideoUploading ones listed in §2. Do not run the integration suite.
5. Run the handoff §10 hygiene greps and include output:
   `grep -rn "lock" src/alphazero-api/AlphaZero.Shared/Infrastructure/Caching/ScopedEvictionProvider.cs`
   (expected: no `lock` statements — `lock` may appear only inside prose identifiers/doc text if
   you chose the F fallback with justification)
   and
   `dotnet test` output lines for the two spec test files showing counts.

---

## 7. Deliverables

1. Refactored `ScopedEvictionProvider.cs` (only this file changed; + the one added test).
2. Class XML doc: the sync protocol, replacing the current lock narrative, with the same
   load-bearing "instance lanes, not AsyncLocal" reasoning preserved verbatim in meaning.
3. Report (can be the commit body or a section in your final message): chosen variant + why;
   thread-safety argument per invariant; grep/build/test output pasted raw.
4. One commit: `refactor(shared): <one-line — what changed> (#33)` containing only the target
   file + test; do not reformat unrelated code; do not touch generate-able artifacts (bin/obj).

If at any point you believe an invariant forces you onto variant E/F, STOP and produce the
justification FIRST, then implement.
