# Per-Issue Agent Runbook (Courses module spec chain)

How to run any single spec from the chain (#29–#35) in **one isolated agent conversation** — a different
AI instance per issue, no shared memory, only the repo + issue tracker as the channel. This runbook
follows the repo's existing conventions: `docs/agents/issue-tracker.md` (claim/resolve/close) and the
`implementation loop` used by `/implement-spec` (worktree/branch → TDD → review → close), compressed
to fit single-issue scope.

Two artifacts per agent:

1. **The kickoff prompt** (§1) — paste into a fresh conversation, fill the two placeholders.
2. **The workflow** (§2) — the phases the agent executes; the prompt instructs the agent to read this file and follow it.

Issue-specific deltas (§3) override the generic text where they differ.

---

## §1 The kickoff prompt (copy per agent, fill ⟨n⟩ and slug)

```text
You are the implementation owner for exactly one issue in this repo:

  ISSUE: ⟨n⟩
  BRANCH: spec/⟨n⟩-⟨slug⟩        (derived from the issue title; created from current main, or from
                                  main + any upstream branch if the issue body says it builds on one)

Do all of this in this one conversation, in this order. Do not wait for me to confirm routine steps;
ask only if the spec itself is ambiguous or contradicts the code you find.

1. CLAIM (tracker contract).
   - gh issue edit ⟨n⟩ --add-assignee @me       ← your first write action to the issue
   - gh issue view ⟨n⟩ --comments               ← you + labels + every comment below it
   - Read docs/agents/issue-tracker.md — it is the convention source for claim/resolve/close.

2. LOAD CONTEXT (read exactly these, in order — nothing else until all are read):
   a. Your issue body (locked spec) and every comment on it.
   b. docs/agents/issue-tracker.md, docs/agents/domain.md, docs/agents/triage-labels.md.
   c. docs/reports/courses-gap-report-v2-2026-10-16.md     ← shared evidence base; the issue cites it.
   d. The files the issue names under src/alphazero-api/Modules/Courses — read them, do not trust the
      issue's line numbers blindly (code may have moved since the spec was written; re-verify every
      claim first and reconcile differences in a short note in step 4, not by silently proceeding).
   e. docs/agents/per-issue-runbook.md §3 — the addendum line for THIS issue adds
      issue-specific guardrails/read-first targets.

3. BASELINE BEFORE TOUCHING ANYTHING.
   - Build + full test suite (dotnet test) from a pristine worktree. If baseline is red, STOP:
     report the failing tests in your plan and confirm they predate your change before starting.
   - git working tree of the worktree must be clean; branch created as above.

4. PLAN THE DIFF — post it to the issue before coding:
   gh issue comment ⟨n⟩ --body "...": one short note containing
   - the seams you will use (HTTP endpoint tests via Courses.Tests.Integration handlers where the
     issue's Testing Decisions say so; handler unit tests with NSubstitute elsewhere);
   - every place where the spec's stated file/line evidence no longer matches current code, with
     the reconciliation you chose (never silent);
   - your commit plan (one commit per behavior/test unit — see §2.4).
   Then start implementation immediately; this note is your entry gate, not an approval request
   unless I intervene.

5. IMPLEMENT (TDD loop unless the issue's Testing Decisions say otherwise):
   - For each slice: failing test on the decided seam first → minimal implementation → refactor →
     tests green. The rule: "only test external behavior" per spec; no test may assert internals
     the spec doesn't declare as contract.
   - Commit per completed slice, message: feat(courses)/chore(courses)/fix(courses): <what> (#⟨n⟩,
     and add "(blocked by #k done)" only when that dependency's acceptance defined the behavior).
   - No scope promises in any commit that the spec's Out of Scope section named out.

6. REVIEW GATE before you call it done (in order):
   - Full dotnet test: green. If red, you fix, you don't hide.
   - Code review of the whole branch diff (use a code-review skill/pathway if your harness has one;
     otherwise self-review against these two questions: [i] every spec "User Story" countable as
     either implemented-with-test or explicitly deferred in Out of Scope — enumerate them; [ii]
     anything touching a normally-live system in the spec's Implementation Decisions — as-is?).
   - Diff hygiene review by eye before PR to catch file selection (no stray debris files, no
     accidental module-wide renames outside what the issue sanctions).

7. SHIP:
   - git push to the branch, open a PR closing the issue:
     body: "Closes #⟨n⟩" first line; then a one-line summary, then test-evidence bullet list
     (tests added, acceptance from issue's Testing Decisions each individually addressable).
   - Do NOT merge unless the repo convention here (docs/agents) allows it; if the merge isn't
     yours, hand me the PR link and stop.
   - If the issue you were blocked on unlocks other issues; close-verification is the tracker's
     job, not yours — just tell me which issues your merge unlocks.

8. REPORT back in conversation (compact): STATUS (DONE / DONE_WITH_CONCERNS / BLOCKED), what
   shipped, decisions you made that the spec didn't pre-empt, surprises, and anything you noticed
   outside your scope ("see something, say something" — one sentence each, don't fix).
```

---

## §2 The workflow (what `spec/n` agents actually do — same phases every issue)

### 2.1 Bootstrap (no implementation)
Claim → read list → baseline build/test → write plan-comment on the issue. **Commit style is
declared before any code exists** so a later hand-off (same issue resumed in another conversation)
doesn't break continuity. Time budget: ~15 min equivalent of work; if reading list exceeds that,
the agent is over-reading (the issue + report + own files is enough).

### 2.2 TDD loop (implementation)
- Slice by spec user-story clusters, not by file (test-first per the issue's **Testing Decisions**).
- The agent's only source of decisions when the spec is silent: existing code patterns; the spec's
  own note "Implementation Decisions" is a hard register of choices — new ad-hoc decisions go into
  the plan-comment on the issue (§1 step 4), not into the agent's head.
- Sibling-ISSUE ring-of-respect: the agent may READ any repo file; it may only MODIFY files whose
  purpose matches its issue. Cross-issue refactors discovered mid-flight get logged in the issue
  comment as a "deferred to its issue" one-liner, never implemented inline.

### 2.3 Review (self + tooling)
Code review of branch diff. The prompt's two review questions are strong and cheap. Rerun the full
test suite as the final gate — the agent's own "tests pass" claim is only verified by CI/the merger.

### 2.4 Ship + close
Branch → PR → `Closes #n`, evidence bullet list. If the harness allows merging, PR is the merge
unit; a follow-on agent waits for you (that is what the native blocked-by edges are for). If not,
agent stops at PR and the orchestrator/me merges.

### 2.5 Failure protocol
- Spec ambiguous → ask in the issue itself (`gh issue comment`), not silently.
- Baseline red, or an upstream dependency still open → `BLOCKED` result; do not unblock by
  re-scoping to something outside the spec.
- Real defect in ANOTHER issue's surface → do not fix; write the observation in the issue comment
  of the owning issue; report in your final result.

---

## §3 Per-issue addendum (read-first + special guardrails, keyed to issue)

| # | Extra read-first targets | Special guardrails / judgment calls |
|---|---|---|
| **#29** CRUD P0 | `CourseSection` internal methods; `Course.UpdateInformation`; soft-delete interceptor config; `AccessControl` ARN patterns | Route spellings ENROLLMENTS (never the Enrollement domain comment); **reuse** — do not re-create commands that already exist, they are orphaned, not missing; sections' child-item soft-delete behavior composes with bitmask stability tests; progress recomputation test is acceptance |
| **#30** Maintenance | The v2-report §2-B list (this is the delete list); every file named | Everything here is **subtractive** — deletions are the spec; "prove it's dead" is real work: grep + zero-reference + full-suite-green. Ticket-secret fail-fast touches shared code (out-of-Courses) — allowed in this issue only for the playback signer composition point |
| **#31** Rename | MassTransit message URN conventions; EF configuration class names; every route string with the typo; `videos` module references to Courses' events | **Recon first, then a decision comment:** investigate the message URN pin path, post your pin strategy in the issue comment BEFORE renaming; in-flight message compat is acceptance; the deploy window / alias route requirement is spec'd — never rename + change payload shapes in the same PR; harmless reconciliation of route aliases OK |
| **#32** Student UX | Existing `GetCourseOverview` pre-request visibility family and its tests; `GetStudentDashboard`; course plans; issue report P1 items 7/8 | **Do not modify existing list/detail endpoints** shape or auth; the new endpoints must not regress anonymous/published visibility rules spoofed by test evidence; completion event idempotency test is acceptance; do NOT touch playback URLs inside the view (spec puts resource identity in a renamed field only) |
| **#33** Cache | Sibling modules' cache wiring precedent (Identity/VideoUploading/Tenants); shared HybridCache + decorator; issue report P2 item 20 | Conservative default = don't cache; explicit landing zone only (catalog / view / subject-list); mutation-eviction wiring is part of the same PR as the cached reads (never reads without evictions); tenant key isolation asserted |
| **#34** Lifecycle | `Enrollment.cs` + `EnrollmentStatus.cs` guards; issue report P2 13/14; spec #29's deactivate endpoint (wired or PR'd) | **Transition matrix table** single source, in the spec; new events wire-pinned same as #31 (no in-flight breakage); scheduler/jitter for expiry: investigate module precedent (implementer's call) — no new cron/lambda deployment artifact |
| **#35** Hardening | All sibling specs' landing zones (that's the real test-target list); issue report §10 test-gap list; per-command validator pattern | No new features — only validators + tests; a fix may fall out (e.g. reorder id-set bug) but scope-tracked separately in the issue comment; saga tests use TauHing/chost MassTransit conventions from `EnrollStudentFromSagaConsumerTests`/`MetadataConsumerTests` precedents; tests for #31-linked behaviors exist only if #31 marked "done" (else write them against current spellings) |

### Test names, and where the repo already has the patterns
Test file naming, integration host, auth stub, each seam — everything listed in the issues' Testing
Decisions exists: `tests/Modules/Courses/Courses.Tests.Integration` (per-endpoint test files, a real
Postgres host, per-test DB reset) and `tests/Modules/Courses/Courses.UnitTests` (handler units with
NSubstitute + FluentAssertions, aggregate tests). If your harness has `/tdd`, use it to run the loop;
otherwise follow §2.2's slice/test-first sequence manually.

---

## §4 How to run the chain (orchestrator cheat sheet)

- Launch in any order that the native dependencies permit — GitHub's `blocked_by` count (shown
  earlier) is the live gate: an agent's issue must show `blocked_by: 0` to start.
- One conversation per issue, one branch per issue; merge normalizes to `main` (or per the repo's
  episodic-flow integration branch, per `docs/agents/issue-tracker.md` conventions).
- Merge order = dependency order: #30 → #31 → {#29, #35, then #32 (+#34)} → #33.
- If a spec's acceptance needs an upstream one (#33 evictions of #29's new mutations, for example),
  verify the blocker's merge landed before opening the downstream; that is what the edge prevents.
