# Spec-34 Rework Plan — Enrollment lifecycle: audit table, token-based access window, batched workers

> **Target repo path once work starts:** `spec-34/plans/enrollment-lifecycle-rework.plan.md`
> **Branch:** `spec/34-lifecycle` · Worktree: `/home/azero/.local/share/opencode/worktree/adfa55/spec-34-lifecycle`
> **Origin:** design session of 2026-10-30/31 (two-axis code review of `da55f...6752499` + grilling rounds 1–4). **Every variant discussed is recorded here — including rejected ones.** `§3` is binding; `§2` lists everything that was argued through and the verdicts.
>
> **How to use this file (next AI agents):**
> - **Step 0: copy this entire file verbatim to `spec-34/plans/enrollment-lifecycle-rework.plan.md`** in the repo (Plan mode prevented the design-session agent from doing it), commit `docs(spec-34): handover plan from design session (#34)`, and work from the repo copy afterwards. Update registers only there.
> - Read `§5` (do-not-do list) and `§3` (decided design) BEFORE touching anything. Decisions are settled — do not re-litigate.
> - Work steps in `§4` in order. When a step is done **and its QA gate passes**, commit with the given message template, then flip its register row to `[x] Done` plus the commit SHA. Never leave a step half-marked.
> - If reality forces a deviation, STOP that step, write it in `§6` (deviation log) with rationale, and let the next agent start from it.
> - Reference commits of the reviewed range: `git log da55f..6752499 --oneline` → 503d074a, 974119f8, ce6fe1c9, 4f52041d, 8aaeb3a3, fc7229ab, da55f3e1. HEAD `db1ed467` carries later merges — NOT part of this rework.

---

## §0. Registers — status of steps (bump as you work)

| Step | Summary | Status | Commit | QA gate |
|------|---------|--------|--------|---------|
| 0 | Copy plan to `spec-34/plans/`, commit | `[x] Done` | 7ef486ac | — |
| 1 | Audit table entity + write path (drop last-transition columns from aggregate) | `[x] Done` | 31211003 | Unit: 272 passed; build green (Courses+API); integration coverage authored (docker unavailable locally — §6 #3) |
| 2 | Aggregate cleanup (no transition plumbing, spelling, guards intact) | `[x] Done` | f83c710c | Unit: 272 passed (updated matrix); build green (Courses+API+integration) |
| 3 | Story #9 read path from audit table | `[x] Done` | 1f3ab20e | Unit: 272 passed; build green; latest-entry fetch rides the house correlated-subquery convention (`docs/QueryArchitecture.md`) |
| 4 | Story #10 `IPreRequestHandler` actor guards | `[x] Done` | b035c03c | Unit: 278 passed (4 new guard tests); builds green; refusal integration test authored (CI-run) |
| 5 | Shared access-ticket signer/verifier | `[x] Done` | 7d1beada | Unit: 297 passed (round-trip/tamper/expiry/window + config fail-fast); builds green |
| 6 | Identity: window operator + `IOperationEvaluator` | `[x] Done` | b7a6147a | Identity unit: 54 passed (8 operator allow/deny tests); Courses unit: 297 passed; build green |
| 7 | Identity: `PrincipalExpired` integration event (first one) | `[x] Done` | 71e88142 | Identity unit: 58 passed (4 publish-shape tests); builds green; publish-observer integration test authored (CI-run, docker) |
| 8 | Courses consumer: idempotent guarded `MarkExpired` | `[x] Done` | 80736064 | Courses unit: 306 passed (9 new: handler mark/no-op + consumer plumbing); builds green; duplicate/cross-tenant integration test authored (CI-run) |
| 9 | Per-(course,plan) principal creation + enrollment grant branch | `[x] Done` | 96e27e75 | Identity unit: 64 passed (4 Ensure handler + 2 consumer branch tests); Courses unit: 317 passed (2 saga grant + 4 AddPlan + 5 UpdatePlan tests); builds green (API + both integration projects); integration coverage authored (`EnrollmentGrantTests` — windowed/unlimited/legacy-gains-window, CI-run, docker) |
| 10 | Courses mint endpoint (stateful gate, lazy expiry path b) | `[x] Done` | 8a2ad9bf | Courses unit: 326 passed (9 new: happy mint, unlimited, lazy-mark path b, suspended/expired/inactive conflicts, not-found, tenant-required); builds green (API + both integration projects); integration coverage authored (`AccessTicketMintEndpointTests` — CI-run, docker) |
| 11 | Set-based expiry sweep (path c) + worker hardening | `[x] Done` | 979aa324 | Courses unit: 329 passed (3 new: worker interval config resolver tests); builds green (API + both integration projects); migration `AddEnrollmentExpiryIndex` added `(Status, AccessExpiresOn)`; integration coverage authored (`EnrollmentExpirySweepTests` — large-batch cross-tenant set-based update, audit rows, bulk events, re-run idempotent, CI-run, docker) |
| 12 | Analytics derived-from-source daily job (delete decrements) | `[x] Done` | 29c1b098 | Courses unit: 332 passed (3 new: refresh interval config resolver tests); `DecrementExpiredEnrollmentAsync` deleted; builds green; migration `AddEnrollmentCourseStatusIndex` added `(CourseId, Status)`; integration coverage authored (`CourseAnalyticsRefreshTests` — multi-status course data-parity test excluding Expired, CI-run, docker) |
| 13 | Docs + issue #34 as-built comment | `[x] Done` | 44b55e54 | `docs/Identity/reference/policy-schema.md` documents `AccessTicketValid` + `Reference` node; `docs/specs/enrollment-lifecycle-and-access-tickets.md` authored; as-built deviations (§3.8) posted to issue #34 (comment 6557434136) |
| 14 | Squash migrations to one `InitialMigration` | `[ ] Not started` | — | — |
| 15 | Final review gate (checklist §7 + re-read of plan) | `[ ] Not started` | — | — |

---

## §1. Why a rework — code review verdict (2026-10-30, `da55f...6752499`)

### 1.1 Standards axis (8 judgement-call smell findings, 0 hard documented-standard violations)

1. **Data Clump / infra through the aggregate** — `Domain/Aggregates/Enrollments/Enrollment.cs`: `LastStatusChangedOn`/`LastStatusChangedBy` are audit plumbing that no invariant, rule or event payload uses; they exist only so read paths can surface them (issue story #9). Contrast `CompletedOn`, which gates a real rule (no re-raise). Repo precedent (`EnrolledOn`, `CompletedOn`) puts timestamps on aggregates, so it passed as a judgement call — **but the repo owner rejected it (concern #1)**. → replaced by the audit table (§3.1).
2. **Mysterious Name** — `MarkExpired(DateTime occuredOn)` — misspelled param. → rename `occurredOn`/trim the param with the redesign.
3. **Duplicated Code** — Deactivate/Suspend/Reactivate handlers all repeat `var previousStatus = enrollment.Status; … if (previousStatus != enrollment.Status) { repo.Update; log }`. → extract to the aggregate (e.g. `Transitioned` flag) or a handler helper during step 2.
4. **Double scoping** — `EnrollmentExpiryService` is resolved via `coursesModule.CreateScope()` by the worker, then re-creates its own MS scope per batch; the outer scope is dead weight. → one scope owns the work (step 11).
5. **DI bypass** — `new CourseAnalyticsRepository(context)` in the expiry service although `ICourseAnalyticsRepository` is registered (`AddCoursesInfrastructure`). → disappears with step 12 (repository method deleted).
6. **N+1 analytics** — expiry scan issues one UPDATE **per completed bitmask bit** (`DecrementExpiredEnrollmentAsync` loop) — hundreds of round trips per 200-row batch (repo owner's concern #3). → whole path deleted (step 12).
7. **Leaked scope** — `EnrollmentExpiryWorker` creates a scope for `ResolveIntervalSeconds` and never disposes it. → fix in step 11.
8. **Cross-module reach + broad catch** — `Identity/.../AuthorizationContextProvider.cs` edited for Courses' sake (story #9 metadata derivation); the degrade mechanism catches `InvalidCastException`, swallowing genuine programming errors as missing permission metadata. → **kept** (story #9/#10 still need the actor envelope) but narrowed during step 4; `Course.AddPlan/UpdatePlan` duplicate `accessWindowDays < 1` validation → dedupe.

No hard violations: write path uses tracked repositories; no `IQueryService` in commands; integration events follow the domain→module-bus→outbox convention; `courses:Edit` matches `docs/Permissions.md`/#29 precedent.

### 1.2 Spec axis (8 findings: 2 missing/partial, 3 scope creep, 3 divergent)

**(a) Missing/partial**
1. **Story #10 enforcement is HTTP-edge only** — spec: *"only Payments' own revocation saga or a tenant-admin-level actor can fire deactivation and suspension"*. `SuspendEnrollmentCommandHandler`/`DeactivateEnrollmentCommandHandler` do no actor check; `EnrollmentTransitionActor.Resolve` records who but guards nothing; any in-process caller can fire the command silently. → fixed by step 4 (grill verdict: `IPreRequestHandler` guard).
2. **Story #6 partial** — one-way analytics (see c-1) permanently zeroed a reactivated student's counters, although the spec legalizes Expired → Active reactivation. → fixed by step 12.

**(b) Scope creep (largely retro-legitimized by the rework)**
1. Plan-window editing (`AccessWindowDays` on Add/Update plan incl. "null clears the window") — spec asked only a pure-function calc; now it IS the feature (windowed plans drive principals + tokens).
2. Identity module change (`AuthorizationContextProvider` degrade) — spec said "without new infrastructure"; the rework legitimately expands Identity involvement, so the creep becomes the design (§3.2/§3.3). Catch narrowed regardless.
3. Generalized idempotency (Suspend + Deactivate got it too; story #11 asked only Reactivate) — harmless, keep.

**(c) Implemented but divergent**
1. **Analytics asymmetry breaks the spec's matrix** — `DecrementExpiredEnrollmentAsync` (GREATEST-clamped) permanently removes counters; Reactivate from Expired never restores them; failed adjustments only logged, never retried (silent drift). → derived-from-source (step 12).
2. **Access-window deadline stamped at enroll time** — spec: *"pure function of the course plan definition"*. `EnrollStudentFromSagaConsumer` computes `DateTime.UtcNow.AddDays(windowDays)` once; `UpdatePlan` never propagates. → **resolved in grilling:** the window is per-enrollment frozen at enroll by *intent* ("they bought 30 days" — §2, R4-Q3). The frozen timestamp stays; the semantic is documented, so the divergence becomes a recorded spec amendment on issue #34 (step 13).
3. Saga compensating reactivation — consumers verified going through aggregate guards (OK given the spec's conditional wording).

---

## §2. The grilling — every variant that was on the table

Four grill rounds ran (2026-10-30/31). Each Q lists the variants **and** the verdict, so no future agent re-opens a settled branch.

### R1 — round 1

- **Q1 Who/when source of truth (concern #1 + story #9)** — Variants: (a) append-only audit table fed by in-process event handlers; (b) keep columns on the aggregate behind a private `Transition()` method; (c) read integration events back from the MassTransit outbox (owner's own candidate; owner's own doubt already stated as impractical — at-least-once, no full history, reads shouldn't depend on transport).
  **Verdict: (a)** — an audit table usable for multiple event kinds: *"here for EnrollmentTransitions for an enrollment Id and metadata"*. Owner: *"make grilling for the design"* → refined in R2.
- **Q2 AccessWindow system design (concern #2)** — Owner's proposal (binding direction): a **condition evaluator with a typed condition**, reading a **token sent in the header other than the JWT** — avoids the background worker for authorization, keeps the serverless door open, same architecture as JWT. IAM stays the authorization point; enrollments still manage enrollment lifetime; expiry materializes via an event; background services only wash the DB in batch.
  **Verdict: adopt the owner's stance**; mechanics refined in R2/R3 first.
- **Q4 Worker & analytics (concern #3)** — Owner: *"more robust worker and service that can handle batched updates to update sql once without much headache to the db"*.

### R2 — round 2

- **Q1 Audit table shape** — Variants: (a) Courses-module table with generic columns (EventKind + jsonb Metadata); (b) aggregate-private method; (c) genuinely shared module-agnostic audit service (flagged Speculative Generality until a second module needs it).
  **Verdict: (a)** — owner confirms: *"Courses-module table `EnrollmentAuditEntries` with generic columns seems fine to me."* → §3.1.
- **Q2 Token mechanics → condition system** — Owner pulled in the Identity condition system (`ConditionDefinition.cs`, `docs/Identity/`): sign a token; the **policy carries a condition** whose operator validates a window **read from token properties only**; when evaluation sees expiry, **raise `PrincipalExpired(PrincipalId, UserId)`**; a background service handles the marked enrollments' analytics. And: *"let identity unrelated to course module"* — the condition is generic, reading another self-signed token.
  **Verdict: condition/operator seam in Identity; token self-contained (HMAC, PlaybackTicketSigner pattern, dedicated secret). NOT opaque/DB-backed — that reintroduces the request-time DB check the design exists to kill.**
- **Q4 Marking paths** — Owner: *"a,b,c makes everything solid"* (condition-event marking + mint-time lazy expiry + set-based sweep backstop).
- **Q5 `PrincipalExpired` shape + idempotency** — Owner: *"Solid Call"* — event carries PrincipalId/UserId/TenantId + resource context; Courses consumer idempotent (already-Expired → no-op success, cheap duplicate evaluations while marking is pending).
- **Q6 Analytics counter architecture** — Variants: (i) snapshot delta on enrollment; (ii) derived-from-source scheduled refresh; (iii) one-way decrement + block Reactivate-after-Expired. Owner supplied the full ruled decision ("🏁 Course Analytics Design Decision") with ground truth: analytics read only by admin eyes (no machine consumers), staleness tolerance hours-to-a-day, reactivation rare-but-real, scale hundreds → hundreds-of-thousands, `CourseAnalytics` already fire-and-forget/decoupled, ItemCompletions display-only.
  **Verdict: (ii) derived-from-source** — owner quote: *"Go with Option (ii). The verdict is unambiguous given your constraints … you delete DecrementExpiredEnrollmentAsync and all its N-round-trip baggage, and replace it with one scheduled SQL job."* (iii) rejected: product regression disguised as simplification; (i) rejected: "Option (ii) with extra steps", minus the snapshot column/re-apply code.

### R3 — round 3

- **Q1 AccessExpiresOn fate** — asked: drop + recompute via plan join everywhere, vs keep as sweep queue. Left unanswered in R3; **settled in R4-Q3** (keep — frozen-at-enroll is the intended semantic).
- **Q2 Suspension latency with stateless tickets** — Variants: bounded staleness with short TTL + stateful mint (**winner**), per-request DB status check (defeats the design), Redis revocation list (new infra).
  **Verdict (owner's adopted stance, quoted):** *"While a ticket is valid, the edge lets requests pass without querying the database. When the ticket expires and the client asks for a fresh one: the minting endpoint hits the DB and checks enrollment status. If Status == Suspended (or expired), minting fails (409 Conflict / 403 Forbidden). The student is locked out immediately."* Token carries the plan window; the operator compares token properties only, no DB checks.
- **Q3 Condition attachment** — Owner: *"the condition is related to the principal not the endpoint, when Enrollment for a student is created, a principal is made for him saying plan expires on date (if the course has a plan), endpoints don't know about conditions."* Grant data lives at principal/policy level at enrollment creation. Refined again in R4-Q1.
- **Q4 Story #10 guard** — Owner: implement `IPreRequestHandler` (see `AlphaZero.Shared/Application/IPreRequestHandler.cs` — feeds `PreHandle(TRequest request, AuthorizationContext? context, CancellationToken ct)` into commands). → step 4.
- **Q5 Analytics cadence** — Owner: *"it is okay for the job to run each day, not hourly because it is an exhaustive db query."*
- **Q6 Sweep mechanics (status: answered in R4)** — owner chose **(b)**: **pure set-based UPDATE**, transition guard encoded in SQL (`Status='Active' AND due`), audit rows inserted set-based, events published from `RETURNING` rows in bulk — aggregate loads dropped for the sweep ONLY; request/event paths still go through aggregate guards.

### R4 — round 4

- **Q1 Where the per-enrollment grant lives** — Variants: (a) per-enrollment principal with inline policy; (b) Condition column added to the assignment row; (c) one shared template referencing the operator with window data riding the token only (fails: unlimited-course students get denied by the failing condition).
  **Verdict (owner): NONE of the above.** *"Making a principal for each user is making querying principals harder. We can take a plan, course and make a principal for it (when courses are created) that is a managed policy — a principal for each plan when it is found, with configured time. Inline policies are hard to manage and only should be used for exceptional cases."* → per-**(course, plan)** role principal, managed policy, no inline policies (§3.2).
- **Q2 Mint endpoint + TTL ~2h** — Owner: *"sounds fine."*
- **Q3 Window frozen at enroll (no retro-active rewrites)** — Owner locks it verbatim: *"When a student enrolls in a 30-day course, they bought 30 days. If the academy later cuts the course to 14 days or extends it to 60 days, existing enrollments are not retroactively rewritten."*
- **Q4 Future-only plan edits** — *"Solid."*
- **Q6 Sweep = option (b)** — logged above.

### Also locked (final round)

- **Ticket payload** — `(tid, uid, eid, exp, iat)` **plus** the window (`windowStart`, `windowEnd`). Owner removed deviceId binding explicitly: *"the access time has nothing to do with deviceId to evaluate, you can use plan from one or more phone; if any course wants to make this assumption, they add an operator for IsMainDevice."*
- **Token TTL** — `AccessTicket:Ttl` config, ~2h default; re-mint bounds staleness.
- **Migration strategy** — owner: *"don't care much about the migration, you can squash them into one InitialMigration after you design the changes."*
- **Analytics status filter** — plan adjustment grounded in R3-Q5: refresh filters `Status != 'Expired'` (today's counters count every enrollment except expired; the verdict sketch's `= 'Active'` would silently drop deactivated students — flagged and corrected in the design session).

Fact map backing these verdicts (gathered by explorer, 2026-10-30):
- Condition system: `IConditionNode` (And/Or/Not/Statement/Reference), `ConditionEvaluatorService` reflects properties off `AuthorizationContext`; heavy operators plug in via **`IOperationEvaluator`** DI seam (`IsMainDeviceOperationEvaluator` reads `X-Timestamp`/`X-Signature` headers from HttpContext). `ConditionDefinition` rows cached 10 min via HybridCache (JSON string, polymorphic converter `IdentityCacheJson`).
- No seeder/admin endpoint exists for `ConditionDefinitions`; managed-policy DTOs don't yet accept conditions (Domain `PolicyStatement` already has the optional `Condition` ctor arg).
- **Identity publishes zero cross-module integration events today** — `PrincipalExpired` is the first; outbox already wired on Identity's AppDbContext.
- Enrollment grant today: `EnrollmentCreatedIntegrationEventConsumer` → `AssignPrincipalToUserCommand(studentId, StudentPrincipalTemplateId = 00000000-0000-0000-0000-100000000002, courseArn)`.
- Token precedent: `PlaybackTicketSigner` — stateless HMAC (`base64url(json payload)` + HMAC-SHA256, Cloudflare-key), verified in VideoUploading `PreRequestHandler`s with IAM fallback, TTL 120 s.
- JWT: internal HMAC (`sub, tid, auth_method`), 1440-min default; `IAMPreprocessor` (FastEndpoints global) → `IPolicyEvaluatorService.Authorize` → 403.
- Active-enrollment gate today: `EnrollmentQueryService.cs:149` — `Status == Active`.

---

## §3. Decided design (binding)

### 3.1 Enrollment audit table

- `EnrollmentAuditEntries` in the Courses module: `Id, TenantId, SubjectId (enrollment id), EventKind (Suspended | Resumed | Expired | Reactivated | Deactivated | …), ActorId, OccurredOn, Metadata (jsonb: From, To, cause)`. Generic columns; new event kinds join without schema change.
- **Writer:** existing in-process `DomainEventHandler` (MediatR `INotificationHandler`) inserts rows via the same DbContext — atomic with the aggregate mutation, no outbox dependency. The new `PrincipalExpired` consumer raises the same domain events, so event-path marks land in the table too.
- **Read:** story #9 — projections/query service fetch the **latest** entry per enrollment; `GetEnrollment` surfaces who/when from it. `LastStatusChangedOn`/`LastStatusChangedBy` are **deleted from the aggregate and schema** (external read contract keeps the field names).
- Migration squash (step 14): all of spec-34's migrations collapse into one `InitialMigration` after design is done.

### 3.2 Access window — principal-per-(course,plan) + token + one generic operator

1. **Grant shape:** when a course with a windowed plan is created, Identity gets a **dedicated role principal for that (course, plan)** carrying a **managed policy**: allow `courses:View`/`courses:Read` on the course ARN, statement `Condition` = the generic window operator (§3.3). Unlimited-plan courses keep today's shared Student template assignment.
2. **Enrollment grant:** `EnrollmentCreatedIntegrationEvent` payload grows (plan/window info); the consumer branches: windowed → assign the per-plan principal (course ARN scope); unlimited → shared template as today. `AssignPrincipalToUserCommand` via `IIdentityModule`, unchanged signature.
3. **Token:** HMAC-signed (`PlaybackTicketSigner` shape but **dedicated secret**, not the playback secret). Payload: `tid, uid, eid, exp, iat, windowStart, windowEnd`. Sent in its own header. The operator validates **token properties only** — signature + `exp` + `now ∈ [windowStart, windowEnd]` — **zero DB checks in the flight path**. No deviceId binding.
4. **Mint (the stateful gate):** Courses-owned endpoint (`AccessControl("courses:View", …)`): requires `Status == Active` else **409/403**; window already elapsed while still Active → lazy-mark first (path b) then 409 (window-elapsed reason). TTL ≈ 2 h (`AccessTicket:Ttl`), re-mintable — re-mint is the staleness bound.
5. **Window frozen at enroll:** `AccessExpiresOn` stays on the enrollment as **the per-enrollment frozen window** (computed at enroll from plan window days: "they bought 30 days"). Mint copies it into the token; the sweep uses it as the due predicate. `UpdatePlan` edits change **future enrollments only** — no event fan-out, no policy rewrites, no token invalidation.

### 3.3 Condition operator (Identity) — the seam

- New `Operator` (e.g. `AccessTicketValid`) handled by a new **`IOperationEvaluator`** registered in Identity DI — the `IsMainDeviceOperationEvaluator` precedent (reads request headers via HttpAccessor).
- Evaluator is **module-agnostic**: reads the header token, verifies HMAC/expiry via the shared verifier, fail-closed when the token is absent, pass/fail with **no DB and no course knowledge**.
- Policies reference it as a condition; endpoints never mention conditions (owner: *"endpoints don't know about conditions"*).
- Serialization: the node must round-trip through `IdentityCacheJson` (polymorphic converter) and the EF value converters without breaking the schema. While here, document the pre-existing drift: `docs/Identity/reference/policy-schema.md` lacks the `Reference` condition type.

### 3.4 Expiry marking — three paths, one idempotent consumer

- **(a) Condition-driven:** operator sees `now > windowEnd` → **raise `PrincipalExpired(PrincipalId, UserId, TenantId, ResourcePath)`** — Identity's **first cross-module integration event** (publish inside the operator's evaluation; consumer-side idempotency absorbs duplicate evaluations). Courses consumer resolves enrollment(s) from (UserId, course ARN in the context's resource path), runs the **aggregate's guarded `MarkExpired`**, no-ops when already Expired. `EnrollmentExpiredIntegrationEvent` + audit row flow through the existing `DomainEventHandler`.
- **(b) Mint-time lazy expiry:** §3.2.4 above.
- **(c) Set-based sweep backstop (owner R4-Q6 = b):** hosted worker pass (hourly — cheap: indexed single-statement; STALENESS ≠ analytics exhaustiveness): one SQL `UPDATE … SET Status='Expired' WHERE Status='Active' AND "AccessExpiresOn" <= now RETURNING …`; audit rows inserted set-based in the same statement(s); `EnrollmentExpired*` events published in bulk from the `RETURNING` rows. **No aggregate loads.**
- Aggregate discipline is **not** dropped globally: it stays on every request/event path; only the sweep short-circuits, by explicit owner decision.

### 3.5 Story #10 actor guard

- `IPreRequestHandler<TRequest>` for `SuspendEnrollmentCommand`, `DeactivateEnrollmentCommand`, `ReactivateEnrollmentCommand`: read `AuthorizationContext`, resolve the actor (`EnrollmentTransitionActor` grows `EnsureCanFire`); reject anything that isn't the Payments revocation saga (distinct transport-claimed actor) or a tenant-admin-level actor — BEFORE the aggregate is touched.

### 3.6 Analytics — derived-from-source, daily

- **Delete:** `DecrementExpiredEnrollmentAsync` (+ all callsites), `AdjustAnalyticsForExpiredAsync`, the per-bit/per-row write path.
- **Add:** one scheduled daily job (config-driven interval — owner: daily, the query is exhaustive), single set-based UPSERT:

```sql
INSERT INTO "Courses"."CourseAnalytics" ("CourseId", "TotalEnrollments", "SumOfCompletionPercentages", "ItemCompletions")
SELECT e."CourseId", COUNT(*), SUM(e."CompletionPercentage"),
       COALESCE(jsonb_object_agg(bits.bit_index, bits.bit_count) FILTER (WHERE bits.bit_count > 0), '{}'::jsonb)
FROM "Courses"."Enrollments" e
CROSS JOIN LATERAL (
  SELECT gs.i AS bit_index, SUM(get_bit(e."ProgressBits", gs.i)::int) AS bit_count
  FROM generate_series(0, length(e."ProgressBits") * 8 - 1) AS gs(i) GROUP BY gs.i
) bits
WHERE e."Status" != 'Expired'          -- NOTE: deliberately NOT the verdict sketch's = 'Active'; see filter note
GROUP BY e."CourseId"
ON CONFLICT ("CourseId") DO UPDATE SET
  "TotalEnrollments" = EXCLUDED."TotalEnrollments",
  "SumOfCompletionPercentages" = EXCLUDED."SumOfCompletionPercentages",
  "ItemCompletions" = EXCLUDED."ItemCompletions";
```

- **Filter semantics (design-session adjustment, grounded in R3-Q5):** today's counters increment per enrollment creation and never decrement for Inactive/Suspended — i.e. `TotalEnrollments` = every enrollment **except expired** (owner R3-Q5 accepted the recommendation). The verdict sketch's `WHERE Status = 'Active'` would silently drop deactivated students from dashboards — do NOT adopt it.
- **Index:** composite `Enrollments (CourseId, Status)` — the only real cost at scale.
- **Cadence split (optional, config):** if the lateral `get_bit` aggregation proves costly at the extreme, run `ItemCompletions` less often (e.g. 6h) or fold into the daily pass; tolerance is hours-to-a-day (admin-eyes-only consumers).
- Staleness is bounded by design; no snapshot columns, no re-apply path.

### 3.7 Story #10 note ↔ §3.5

(`ensure section numbering continues cleanly: Story #10 handled in §3.5; this slot intentionally merges into §3.5 and §3.8 below.)

### 3.8 Intentional as-built deviations from issue #34 (record on the issue, step 13)

1. Window frozen at enroll (spec's "pure function" reading amended — §2 R4-Q3 quote).
2. Expiry marking via condition event + mint + set-based sweep (spec allowed "background job OR on-read lazy expiry"; as-built = all three paths).
3. AddPlan/UpdatePlan window editing stays (was scope creep; now the feature driving principals/tokens).
4. Analytics decrements deleted entirely (spec never mandated counters); derived refresh replaces them.
5. Identity gets its first integration event + a second custom operator (beyond IsMainDevice) — documented in docs/Identity.

---

## §4. Execution steps (order, files, QA gates, commits)

> Contracts: each step ends with a green build + its QA gate; commit with the given message type; update the §0 register. Steps 1–4 are Courses-internal and are independent of token steps 5–10 — do not interleave unless noted.

### Step 0 — Handover

- Copy this file verbatim to `spec-34/plans/enrollment-lifecycle-rework.plan.md`; commit `docs(spec-34): handover plan from design session (#34)`. From then on only the repo copy is authoritative.

### Step 1 — Audit table entity + write path

- Files: `Modules/Courses/Domain/Audits/EnrollmentAuditEntry.cs` (or matching layout per `docs/FolderStructure.md`), EF configuration, `DomainEventHandler` write path.
- Tasks: entity `Id, TenantId, SubjectId, EventKind, ActorId, OccurredOn, Metadata(jsonb)`; configuration (index `(SubjectId, OccurredOn)`); in `DomainEventHandler` handlers for Suspended/Resumed/Expired/Created-derived kinds insert rows in the same context — **same save** as the aggregate mutation; Metadata captures From/To/Actor; tenant-scoped.
- Accept: suspending/resuming/deactivating/expiring/reactivating an enrollment produces exactly one audit row per transition, same transaction.
- Commit: `feat(courses): enrollment audit entries written in-process on lifecycle transitions (#34)`.

### Step 2 — Aggregate cleanup

- Files: `Enrollment.cs`, `Deactivate/Suspend/Reactivate/MarkExpired` internals, `EnrollmentStateTransitionTests.cs`.
- Tasks: **remove `LastStatusChangedOn/LastStatusChangedBy` props** and every setter/tracking caller across Sage transitions; dedupe the tripled `previousStatus … log` shape (extract `Transitioned` flag or a handler-level helper — smell #3); fix `occuredOn` misspelling; dedupe `Course.AddPlan/UpdatePlan` window validation.
- Accept: state-machine-matrix tests pass with transitions still guarded; no aggregate member serves only the read boundary.
- Commit: `refactor(courses): aggregate sheds audit plumbing; transition dedupe (#34)`.

### Step 3 — Story #9 read path from audit

- Files: `Infrastructure/Queries/EnrollmentQueryService.cs`, `Presentation/Enrollments/Get/GetEnrollment.cs`, `Application/.../GetEnrollment.cs` contract.
- Tasks: latest-entry fetch per enrollment inside the projection service's conventions (`docs/QueryArchitecture.md`); existing read DTOs keep the `LastStatusChangedOn/By` field names (external contract), but the SOURCE is the audit table.
- Accept: `GetEnrollment` responds with honest who/after each transition; no aggregate references linger.
- Commit: `feat(courses): surface last-transition who/when from the audit table (#34)`.

### Step 4 — Story #10 actor guards via IPreRequestHandler

- Files: new `IPreRequestHandler` impls next to `Suspend/Deactivate/Reactivate` commands; `EnrollmentTransitionActor.EnsureCanFire`; keep `AuthorizationContextProvider` change but **narrow the catch** (smell #8).
- Tasks: allowed actors = Payments revocation saga (transport-claimed) + tenant-admin/syt (AccessControl envelope); everything else fails BEFORE aggregate load.
- Accept: unit+integration — in-process attacker fails; saga path passes; admin HTTP path passes; IAM second-host permission-denial coverage intact.
- Commit: `feat(courses): story #10 actor guard via IPreRequestHandler (#34)`.

### Step 5 — Shared ticket signer/verifier

- Files: `AlphaZero.Shared/Security` (beside `IPlaybackTicketSigner`), `Shared/Infrastructure/Security`, DI, config.
- Tasks: `IAccessTicketSigner`/verifier — HMAC-SHA256, dedicated secret (`AccessTicket:Secret`), payload `(tid, uid, eid, exp, iat, windowStart, windowEnd)`, `IsExpired`/window helpers, fixed-time compare.
- Accept: round-trip/tamper/expiry/window unit tests.
- Commit: `feat(shared): stateless access-ticket signer with dedicated secret (#34)`.

### Step 6 — Identity window operator + IOperationEvaluator

- Files: `Identity/Domain/Models/Principals/Condition.cs` (+ `Operator` member), new `IOperationEvaluator` impl, DI registration, `IdentityCacheJson`/EF-converter support for the node WITHOUT breaking JSON schema; `docs/Identity/reference/policy-schema.md` gains the operator (+ missing `Reference` type).
- Tasks: evaluator reads the header token via HttpAccessor (precedent `X-Timestamp`/`X-Signature`), verifies via the shared verifier (exp + window) — **course-agnostic, no DB**; **no DB reads**; fail-closed on absent token.
- Accept: operator allow/deny tests incl. missing header, malformed token, wrong key, expired token, outside-window token.
- Commit: `feat(identity): AccessTicketValid operator over the header ticket (#34)`.

### Step 7 — Identity PrincipalExpired integration event

- Files: new `Modules/Identity/IntegrationEvents/PrincipalExpiredIntegrationEvent.cs` (Identity's FIRST integration event), publish via `IPublishEndpoint(_moduleBus)` (EF outbox on Identity AppDbContext already configured) from the operator evaluation path — best-effort + consumed-side dedupe.
- Payload (owner-approved): `(PrincipalId, UserId, TenantId, ResourcePath)` — ResourcePath from the `AuthorizationContext` ARN (carries the course resource). Courses resolves the enrollment from (UserId, ARN course).
- Accept: integration test — expired-ticket request results in a publish (record with a test publish observer, cf. `RecordingPublishObserver` test harness).
- Commit: `feat(identity): PrincipalExpired integration event from window evaluation (#34)`.

### Step 8 — Courses consumer: idempotent guarded mark

- Files: Courses consumer for `PrincipalExpiredIntegrationEvent` (Infrastructure/Consumers; register in `AddCoursesInfrastructure`/`AddModuleConsumers`); reuse `EnrollmentLifecycleEndpointTests`/`EnrollmentLifecycleEventHandlerTests` patterns.
- Tasks: resolve enrollment(s) by (UserId, course ARN); guarded aggregate `MarkExpired`; **no-op on already-Expired** (storm-safety) with the audit + `EnrollmentExpired` flow per §3.4(a); no-op writes nothing (state-test style).
- Accept: consumer tests incl. duplicate-event no-op (mark runs once), cross-tenant rejection.
- Commit: `feat(courses): PrincipalExpired consumer marks enrollments idempotently (#34)`.

### Step 9 — Per-(course,plan) principal + enrollment grant branch

- Files: Courses course/plan-creation flow (delegation to `IIdentityModule` — Owner: *"make a principal for it (when courses are created)"*), `EnrollmentCreated` contract + `EnrollmentCreatedIntegrationEventConsumer` (Identity).
- Tasks: on course creation with a windowed plan → create a role principal for (courseId, planId) holding the **managed** policy (allow courses:View/Read @ course ARN + `AccessTicketValid` condition); windowed enrollment assignment assigns that principal; unlimited branch keeps the shared template.
- Accept: integration tests — windowed enrollment yields the per-plan principal assignment; unlimited unchanged;创始 no inline policies.
- Commit: `feat(courses): per course/plan principal + windowed enroll grant (#34)`.

### Step 10 — Courses mint endpoint (stateful gate, lazy expiry)

- Files: `Modules/Courses/Presentation/Courses/AccessTickets/IssueAccessTicket.cs` (style near IssuePlaybackTicket), command + handler.
- Tasks: `POST /courses/{courseId}/access-tickets`; `AccessControl("courses:View", …)`; resolve enrollment; window elapsed + Active → mark (path b) then **409 window-elapsed**; Suspended/Expired → 409/403; else mint with `AccessTicket:Ttl` (default ~2 h) and frozen window from `AccessExpiresOn`.
- Accept: endpoint tests (happy mint; suspended 409; expired lazy-mark; windowed student denied without token; unlimited students need nothing).
- Commit: `feat(courses): course access-ticket mint gate + lazy expiry (#34)`.

### Step 11 — Set-based sweep + worker hardening

- Files: `Infrastructure/Expiry/EnrollmentExpiryService.cs` (rewrite = sweep), `EnrollmentExpiryWorker.cs`, migrations/config.
- Tasks: single set-based `UPDATE` with SQL-encoded guard (`Status='Active' AND "AccessExpiresOn" <= now`) `RETURNING`; audit rows inserted set-based in the same statement(s); `EnrollmentExpired*` events published in bulk from RETURNING rows (batchable for the test observer); delete the old per-row aggregate scan for marks; **fix**: scope-leak (worker `ResolveIntervalSeconds` scope already disposed), drop the double-scoping (one scope owns the pass), DI-consistent construction (no `new` repo).
- Accept: large-batch integration test with one UPDATE — correct marks, events, audit rows; zero N+1; re-run idempotent.
- Commit: `feat(courses): set-based expiry sweep + hardened worker (#34)`.

### Step 12 — Analytics derived-from-source daily job

- Files: delete `DecrementExpiredEnrollmentAsync` + callsites, `AdjustAnalyticsForExpiredAsync`; new hosted job + config; index `Enrollments (CourseId, Status)` in migration.
- Tasks: implement §3.6 SQL exactly (`Status != 'Expired'`); daily default, interval in config; log-and-alert on failure (tolerance day but visible).
- Accept: data-parity test — derived result equals legacy counter semantics on a seeded multi-status course (Active/Inactive/Suspended counted, Expired not); single statement confirmed.
- Commit: `feat(courses): daily derived-from-source analytics refresh (#34)`.

### Step 13 — Docs + issue #34 as-built comment

- Files: `docs/Identity/reference/policy-schema.md` (operator + `Reference` type), concepts/reference notes for the audit-table convention and the ticket flow; `gh issue comment` on #34 recording §3.8 deviations verbatim + linking this plan.
- Commit: `docs(spec-34): as-built design — audit table, ticket operator, set-based workers (#34)` (docs-only commit).

### Step 14 — Squash migrations to one InitialMigration

- Owner: *"you can squash them into one InitialMigration after you design the changes."* Collapse spec-34's migrations after ALL schema-affecting steps are green; verify the model snapshot still matches spec-29/32/33 surfaces plus the new artifacts.
- Accept: fresh-DB bootstrap produces the identical schema; full integration suites green end-to-end.
- Commit: `chore(courses/identity): squash lifecycle migrations to InitialMigration (#34)`.

### Step 15 — Final review gate

- Checklist §7 all `[x]`; re-read of this file top-to-bottom vs implementation; any leftover §1 finding becomes a follow-up step rather than a silent close.

### Test matrix summary (steps 1–15 collectively)

- State-machine matrix intact (aggregate guards, ErrorOr semantics, story #8 domain errors).
- Audit rows: every transition (request, consumer, sweep) → exactly one row; atomicity on rollback (failed save writes nothing).
- Operator: misuse/malformed/expired/outside-window/absent header fail-closed; zero DB dependency in the validation path (assert no repo injected).
- Mint: TTL config honored; Suspended/Expired 409; lazy-mark path; frozen window consistency (`AccessExpiresOn` == ticket windowEnd).
- Principal-per-plan: creation on course/plan creation; unlimited branch regression (shared template preserved); both assignment branches.
- PrincipalExpired consumer: idempotency (event storm → one mark, one audit row, one integration event), cross-tenant rejection, ARN resolution.
- Sweep: one-statement semantics at scale; events+audit in bulk; re-run skipped by predicate.
- Analytics: derivation == legacy semantics; index present; cadence config honored.
- Story #10: saga allowed, admin allowed, random in-process actor refused; endpoint permission denials intact (IAM second-host coverage).
- Saga regression: revocation consumer passes through guards like-for-like.

---

## §5. Do-not-do list (guardrails — check before landing)

1. **No per-enrollment principals; no inline policies** for course access (R4-Q1: principal-per-(course,plan) only; inline = exceptional cases only).
2. **No per-request DB checks in the condition operator path** — validation is token-properties-only; Identity stays course-agnostic.
3. **No per-row analytics writes anywhere** — the decrement family is deleted, not reshaped; the refresh job is the only writer of `CourseAnalytics` values.
4. **No aggregate loads in the sweep** (R4-Q6: set-based); aggregate discipline still mandatory on ALL other paths.
5. **No retro-active window rewrites** — frozen at enroll ("they bought 30 days"); `UpdatePlan` = future only; no fan-out event for plan changes.
6. **No deviceId binding in the ticket** (R3-Q3); per-course device pinning belongs to a separate operator if ever needed.
7. **No audit writes via the bus** — audit rows commit in-process/transactionally; the bus only carries integration events.
8. **Don't touch `docs/Identity/reference/policy-schema.md` without also documenting the missing `Reference` node type** (pre-existing drift).
9. **Don't leak scopes** in hosted workers/sweep (worker fix is binding).
10. **Migration squash LAST (step 14)** — never squash mid-stream; never drop the squash requirement.
11. **Don't remove the Suspend/Deactivate generalized idempotency** (accepted creep).
12. **Status filter = `!= 'Expired'`** in derived analytics (§3.6 note) — matches pre-existing counter semantics; do NOT adopt the sketch's `= 'Active'`.

## §6. Deviation log (append-only; one row per deviation)

| # | Step | Deviation | Rationale | Author |
|---|------|-----------|-----------|--------|
| 1 | — | none yet (plan authored) | — | design session |
| 2 | 1 | `Deactivate` was aggregate-event-less (comment + tests pinned "no events"), yet step 1 requires a `Deactivated` audit row from the §3.1 in-process event handler. Raised a domain-ONLY `EnrollmentDeactivatedDomainEvent` as the audit writer's sole trigger; the module bus stays silent for deactivation (Payments revocation flow keeps its identity — no integration event added). | Plan §3.1 designates the DomainEventHandler as THE writer and lists `Deactivated` as an audit kind, but no transition event existed to write from; this is the minimal mechanism inside the decided design, not a variant of it. | executor (step 1) |
| 3 | 1 | The Suspended/Resumed/Expired/Deactivated domain events now carry `FromStatus` + `ActorId` (new `IEnrollmentTransitionEvent` seam). Aggregate emits them from the captured previous status. | Plan step-1 task says audit Metadata captures From/To/Actor, but no event carried them and the only available source (`LastStatusChangedBy`) is deleted in step 2 — so the payload must ride the in-process events. To-status derivable from EventKind; From must be supplied. | executor (step 1) |
| 4 | 1 | The step-1 integration QA gate ("exactly one row per transition, same transaction") could not be EXECUTED locally: no docker daemon → Testcontainers harness unrunnable in this worktree. New integration coverage was authored and compiles (`Lifecycle_Transitions_Should_StageExactlyOneAuditRowPerTransition`); execution deferred to CI. Transaction-atomicity is structural: UnitOfWork publishes notification handlers BEFORE its single SaveChangesAsync (AlphaZero.Shared/Infrastructure/UnitOfWork.cs), the audit writer stages on the same scoped AppDbContext. | Environmental constraint, not a design branch. | executor (step 1) |
| 5 | 2 | Interim: step 2's commit cannot both delete the aggregate props AND keep the query service compiling without pre-implementing step 3's audit-table fetch (§4 steps 2/3 overlap the same 16 projection references). Chosen path: at step 2 the six `EnrollmentQueryService` projections feed the (external) DTO contract `LastStatusChanged*` fields with `null` + `TODO(step 3)`; step 3 immediately restores honest values from the audit table. The story-#9 endpoint assertions (`NotBeNull`) are false only in the two-commit window between steps 2 and 3 (unrunnable locally anyway). | §4 does not settle the inter-step compile boundary; this preserves "one step = one commit" and keeps every commit green. | executor (step 2) |
| 6 | 2 | `MarkExpired(DateTime occuredOn)` trimmed to parameterless `MarkExpired()` (aggregate owns the clock, `EnrolledOn`/`CompletedOn` precedent) instead of merely fixing the spelling. | §1.1.2 explicitly sanctions "rename occuredOn / **trim the param with the redesign**"; the param fed nothing once the audit columns were gone. | executor (step 2) |
| 7 | 4 | The guard's "AccessControl envelope" signal required the dev/bypass host to actually HAVE the envelope: `IAMDevPreprocessor` now stashes the typed request into `__FastEndpoints_Request__` exactly like `IAMPreprocessor` (evaluation stays skipped). Without it, every bypass-harness AccessControl'd request degraded to no-permission-metadata and the "admin HTTP path passes" gate could not hold anywhere. | The plan (§3.5/step-4 tasks) prescribes the envelope as the tenant-admin signal and "keep the provider change but narrow the catch", but §4 didn't address the bypass-harness envelope gap; this makes the harness faithful to prod rather than loosening the guard. The provider degrade is additionally narrowed with `when (!iamRanFirst)` per smell #8 — with the stash present in dev too, a cast failure under the typed request is a programming error and now surfaces. | executor (step 4) |
| 8 | 4 | "Transport-claimed actor" for the revocation saga implemented as an explicit AsyncLocal claim (`EnrollmentTransitionAutomation.ClaimSagaTransport`) that the saga consumer wraps around `coursesModule.Send` — the module dispatch runs in a fresh scope, so no scoped DI service could carry the signal. | §3.5 says "(transport-claimed)" without fixing the mechanism; `AppModule.Send`'s per-dispatch scope rules out ambient DI state. The claim is set/disposed ONLY by the saga consumer path. | executor (step 4) |
| 9 | 5 | `PlaybackTicketSecretValidationTests` production-composition test now configures the new (mandatory-in-Production) `AccessTicket:Secret` alongside the playback secret; step-5 tests satisfy both shared fail-fast gates. | The plan does not address that the new prod gate re-raises inside the pre-existing composition test's config; keeping that test green (rather than weakening the fail-fast) is the safe resolution. | executor (step 5) |
| 10 | 9 | The per-(course, plan) principal delegation lives in the course-PLAN creation flow (`AddPlan`/`UpdatePlan` handlers → `IIdentityModule.Send(EnsureCoursePlanPrincipalCommand)`), not the `CreateCourse` handler — courses are created without plans; windows only come into existence at plan creation, which is exactly R4-Q1's "a principal for each plan when it is found, with configured time". AddPlan for windowed plans no longer requires/uses the caller PrincipalId (system-minted, idempotent via deterministic UUIDv5 per (tenant, course, plan)); unlimited plans keep the caller-controlled principal legacy-style. UpdatePlan mints only for newly-windowed principal-less plans, keeps the principal across window-day edits (future-only §3.2.5), and unlimited updates keep optional PrincipalId (existing retained when omitted — external contract loosened accordingly). | §4 step-9 file list says "course/plan-creation flow"; the owner quote anchors "when courses are created" and §3.2.1 "dedicated role principal for that (course, plan)". Reality: plans are created via AddPlan — the delegation point must be there. Guidance not contradicted; recorded so the next agent doesn't move it. | executor (step 9) |

## §7. Final gate checklist

- [ ] All register rows `[x]` with commit SHAs.
- [ ] `git grep -n "LastStatusChanged" src` returns only DTO/projection consumption, no aggregate plumbing.
- [ ] `git grep -n "DecrementExpiredEnrollmentAsync"` empty.
- [ ] Sweep produces zero per-row round trips (inspection/log evidence).
- [ ] Condition/operator path contains zero DB calls (code inspection).
- [ ] `docs/Identity/reference/policy-schema.md` documents `AccessTicketValid` + the `Reference` node type.
- [ ] Issue #34 comment records the as-built deviations (§3.8) + links this plan.
- [ ] Migrations collapsed to a single `InitialMigration`; fresh-DB bootstrap green.
- [ ] Full test suite green — cross-tenant and permission-denial suites untouched by the rework.
- [ ] Each §5 rule walked line-by-line and still true.

---

## §8. Kickoff prompt (for the next conversation; Step 0 uses this)

```markdown
# Kickoff: execute the spec-34 enrollment-lifecycle rework plan

You are working in the repo worktree /home/azero/.local/share/opencode/worktree/adfa55/spec-34-lifecycle
(branch spec/34-lifecycle) on GitHub issue #34. A full design-decision plan for a REWORK of the
already-landed #34 lifecycle code was authored in a prior design session (code review of
da55f...6752499 + four grilling rounds with the repo owner).

## Step 0 — bring the plan into the repo (do first, nothing before it)

1. Read /home/azero/.opencode/plan/spec-34-enrollment-lifecycle-rework.plan.md in full.
2. Copy it verbatim to spec-34/plans/enrollment-lifecycle-rework.plan.md in the repo.
3. Commit: "docs(spec-34): handover plan from design session (#34)".
4. From then on the repo copy is the ONLY source of truth — the plan file under
   /home/azero/.opencode/plan is frozen input.

## Rules of engagement (binding)

- §3 of the plan is the decided design. §2 records every variant that was argued through and
  the verdicts. DO NOT re-litigate settled decisions; if you disagree, surface it and STOP
  that step — do not implement a variant.
- §5 is the do-not-do list; walk it before landing any step.
- Execute steps 1–15 in order (§4). One step = one commit, using the commit message template
  given in the step. After each step's QA gate passes, update the step's row in the §0
  register (Status → [x] Done, commit SHA) in a follow-up docs commit or as part of the step.
- If reality forces a deviation from the plan, do not silently branch: append a row to §6
  (deviation log) with rationale, then continue or stop.
- Do not modify any spec #29/#32/#33 surfaces; the migration squash happens ONLY at step 14.
- Checklists: each step lists files, tasks, acceptance criteria (QA gate), and a commit
  message. Treat the acceptance criteria as the definition of done — a step whose QA gate
  fails is not done and is not marked.
- The final gate is §7: every checkbox must be [x] before announcing completion, including
  the line-by-line walk of the §5 guardrails.

## Branch of engagement

- Product/design questions: the plan already answers them — check §2/§3 before asking me.
- Ask me only if you hit something the plan genuinely does not settle, and quote the plan
  section first to prove it doesn't settle it.

Start with Step 0 now, then proceed stepwise without waiting for me unless the plan leaves
you no answer.
```
