# Payments Module — Requirements (MVP)

**Status**: Agreed 2026-11-12. Grilling session complete — design frontier empty.
**Source of truth** for what the module must achieve and every product decision made along the way.
Companion docs (queued): `GLOSSARY.md` (exists at repo root), `docs/adr/0002…0004` (drafts in Appendix A).

---

## 1. Why this module exists

AlphaZero's monetization today is offline: access codes sold over the counter at physical Libraries (Sale). This module adds the **online economy**: students pay by card for course access, Academies earn their share, the Platform takes a Commission Share — and every monetary event is recorded in an auditable Ledger.

**MVP goals:**

1. **Card purchases that grant access** through the *exact same pipeline* as access-code redemption — one grant mechanism, two funding sources.
2. **Revenue split** between Platform and Academy, allocated per Purchase in the Ledger; Academies paid via recorded Settlements.
3. **Operations dashboard** — Platform overview plus Academy-scoped views, with the five MVP analytics (§10).
4. **Production-grade money integrity** — double-entry, append-only Ledger; idempotency on every step; full traceability of every transaction.
5. **Compliance-safe go-live** — PSP-agnostic architecture; online checkout shipped behind a go-live flag; access codes remain the live economy until a compliant PSP account exists.

---

## 2. Compliance context (decided 2026-11-12)

- **Sanctions are lifted**: US comprehensive sanctions on Syria ended 2025-06-30 (E.O. 14312); Caesar Act repealed 2025-12-18; State Sponsor of Terrorism rescinded 2026-08-24. EU lifted all economic sanctions May 2025. List-based sanctions remain only on specific persons.
- **PSP policy is the remaining blocker**: Stripe's Restricted Businesses list still bars persons "located in, resident in, or a citizen of" Syria (Stripe confirmed Sept 2026: no timeline). All MoRs (Paddle, Lemon Squeezy, …) and regional PSPs (Paymob, PayTabs, MyFatoorah, …) list Syria ✕ for sellers; Mercury prohibits Syria-resident founders. A Turkish-issued card confers no eligibility; Turkey is not Stripe-supported either.
- **Honest conclusion**: there is no legitimate online card path for AlphaZero today while the founder is the resident owner — with or without a foreign entity. Any structure claiming otherwise is the misrepresentation the founder has explicitly ruled out.
- **Decision**: build the module now, PSP-agnostic, go-live gated (see ADR draft 0004). The unlock is trigger-driven: (a) PSP policy change — the space is moving fast (Visa, Mastercard, Google Pay, Revolut all moved on Syria during 2026) — or (b) genuine relocation + entity formation in a supported country (UAE free zone is the natural MENA vehicle), then Stripe US/UAE in the entity's name. At Stage 1(b), spend an hour with sanctions-competent counsel — this document is research, not legal advice.
- **Bridge model (deferred)**: academies with their own PSP accounts collecting their own sales, Platform commission settled by B2B invoice. The Ledger already supports provider-less money events; this slots into Phase 2 without surgery (§12).

---

## 3. Scope

**In scope (MVP)**

- Price Catalog owned by the Payments module (prices for sellable items live here, nowhere else)
- Per-academy online-selling opt-in, set by Platform admins together with the Commission Share
- Hosted-PSP Checkout for authenticated Students, one-time purchases only
- Double-entry append-only Ledger; per-Purchase allocation of Commission Share / Academy share
- Full refunds with automatic access revocation; dispute recording (manual resolution)
- Manual, record-only Settlements; per-Academy Balance Owed
- Dashboard: Platform overview + Academy-scoped views; five MVP analytics
- Stripe adapter (test mode) behind the `IPaymentProvider` interface and a go-live feature flag
- Webhook processing with exactly-once semantics; PSP reconciliation-ready records

**Out of scope (MVP)**

- Recurring billing / subscriptions (Stripe Billing)
- Partial refunds; custom discount engine; promotion codes (PSP-native or otherwise)
- Stripe Connect accounts and auto-payouts to Academies (deferred — see ADR 0002)
- Stripe Tax
- Blogs or standalone Videos as sellables (only Course Plans)
- Guest checkout
- Academy-collects bridge model (Phase 2)

---

## 4. Domain language

Canonical terms live in `GLOSSARY.md`. The load-bearing distinctions for this module:

| Term | Meaning | Do not confuse with |
|---|---|---|
| **Purchase** | Online card purchase by a Student | **Sale** — offline Library-branch code sale; money never touches the Platform |
| **Purchase Activation** | The event turning a completed Purchase into an access grant, via the same pipeline a redeemed code follows | "Redemption" (codes only) |
| **Price Catalog** | Payments-module-owned record of what each sellable costs | Prices on `CoursePlan` (explicitly rejected) |
| **Commission Share** | Platform's % of each Purchase, per-Academy configurable; remainder is the Academy's share; processing fees borne by the Platform's share | PSP fee (an expense line, not the split) |
| **Balance Owed** | Academy's accrued, unsettled share | PSP balance |
| **Settlement** | Recorded transfer of Balance Owed to an Academy, outside the payment provider | "Payout" (reserved for PSP's own concept) |
| **Refund / Dispute** | Full return of a Purchase / chargeback, recorded, resolved by people | — |

---

## 5. Decision record

| # | Decision | Rationale (abridged) |
|---|---|---|
| D1 | One-time purchases only | Plan windows give subscription-like expiry without Stripe Billing's complexity |
| D2 | Sellables = Course Plans only | Only sellable-shaped entity today; `ResourceArn` mechanism already generalizes |
| D3 | Platform is merchant of record; **no Connect accounts**; split allocated internally; Settlements off-provider, manual, record-only | Academies unreachable by PSP payouts; least friction; ADR 0002 |
| D4 | Split: default 25% Platform / 75% Academy; per-Academy configurable; computed on **gross**; PSP fees absorbed by Platform's share | One knob, explainable to Academy owners |
| D5 | Commission % and selling opt-in set by **Platform admins** | Platform is the seller of record; deliberate compliance lever |
| D6 | Grant semantics mirror the plan: `AccessExpiresOn = now + plan.AccessWindowDays`; no window = indefinite | One grant mechanism for codes and payments |
| D7 | Refunds: full only, auto-revoke via revocation saga. Disputes: recorded, surfaced, resolved manually | MVP scope; both recorded in Ledger regardless |
| D8 | Hosted PSP Checkout | 3DS/SCA, wallets, zero PCI surface for free |
| D9 | Checkout is **authenticated-Students-only** | Grant attaches to `UserId`; mirrors redeem's authentication |
| D10 | Multi-currency: one list price per sellable in a base currency (USD initially); PSP Adaptive Pricing presents local currencies; Ledger records presentment + settlement currency + FX rate | "Handled by the PSP itself" = Adaptive Pricing |
| D11 | **Prices, payments, discounts infrastructure live in the Payments module**; Price Catalog entries point at (tenant, course, plan); Academy admins set their own prices | User override of the "price on CoursePlan" recommendation; makes Payments a true bounded context; ADR 0003 |
| D12 | Ledger: double-entry, append-only, immutable | Only design that makes "track every transaction" auditable and reconciliation tractable |
| D13 | Idempotency bar: all four mechanisms (§9) | Minimum for "production-ready ledger" to be true |
| D14 | Dashboard: Platform overview + Academy-scoped views; analytics = revenue by day/academy/product, transaction list w/ filters + drill-down, refund rate, top products, settlement position | Agreed menu; funnels/cohorts deferred |
| D15 | Explicit per-Academy opt-in for online selling (Platform admin) | Seller-of-record lever; pairs with commission config |
| D16 | **Go-live gated**: full module ships; online checkout behind feature flag; Stripe adapter in test mode; access codes remain the live economy | Compliance reality; ADR 0004 |
| D17 | Bridge model (academy-collects) deferred to Phase 2 | Second money direction not yet needed; Ledger design keeps it a slot-in |

---

## 6. Architecture contract

### 6.1 The pipeline (mirror of access-code activation)

```
Student (authenticated, tenant-scoped)
  → Buy on course view (opted-in sellable)
  → Payments: create Checkout Session (idempotent; snapshot of catalog entry)
  → Hosted PSP checkout (3DS, wallets)
  → PSP webhook: checkout.session.completed
      → verify signature → dedupe on PSP event ID
      → Purchase.Paid + Ledger entries + outbox publish   [one DB transaction]
  → PurchaseActivatedIntegrationEvent                      [outbox → bus]
      (EventId, TenantId, PurchaseId, UserId, TargetResourceArn,
       Metadata { plan: <planId> }, AuditReference = Ledger entry ref)
  → Courses: provisioning command (UNCHANGED from code pipeline)
      → idempotency check → strict plan resolution
      → EnsureCoursePlanPrincipal (Identity role principal + managed policy)
      → EnrollInCourse with AccessExpiresOn
      → failures parked as FailedProvisioningRecord, never thrown
  → Grant = Enrollment + TenantUserPrincipalAssignment      [nothing else]
```

From here, **everything is inherited for free**: IAM enforcement on the course ARN, access tickets, triple-expiry architecture, `CourseRevocationSaga`.

### 6.2 Refund path

Refund webhook → verify/dedupe → Ledger reversal → `Purchase.Refunded` → publish revocation-mirror event → **`CourseRevocationSaga`** (deactivate enrollment, remove assignment). Never delete enrollments directly.

### 6.3 PSP-agnosticism

- The module depends on an `IPaymentProvider` adapter interface (create session, verify webhook, capture metadata, refund status, dispute events, fee reporting).
- The Stripe implementation sits behind it; Courses and Identity never learn a PSP exists.
- Go-live = feature flag + live PSP credentials. Flipping it on is configuration, not code.
- The Ledger's provider-less event support (Settlements today) is what makes the Phase-2 bridge model a slot-in.

### 6.4 Integration points (existing code)

| Reuse | Where |
|---|---|
| Module skeleton (schema, DbContext, outbox, `TransactionalBehavior`) | `src/alphazero-api/Modules/Payments/` |
| Money-row pattern (immutable sale record) | Library `SaleRecord` |
| Integration-event mirror | Library `AccessCodeActivatedIntegrationEvent` + `AccessCodeActivatedConsumer` |
| Provisioning command (unchanged) | Courses `ProvisionAccessCode` (+ `FailedProvisioningRecord` parking) |
| Revocation path | Courses `CourseRevocationSaga` |
| Analytics projection + refresh worker pattern | Library `LibraryAnalyticsRefreshWorker` |
| Audit-log pattern | Library `RedemptionAuditLog` / `LibraryAuditLog` |
| Permission wiring | `AccessControl("<service>:<action>", …)` convention |

**Proposed permission strings** (to be ratified into `docs/Permissions.md`):

- Platform: `payments:ManageAcademySettings` (opt-in + commission), `payments:ViewPlatformAnalytics`, `payments:IssueRefund`, `payments:RecordSettlement`
- Academy: `payments:ManageCatalog` (own tenant), `payments:ViewAnalytics` (own tenant)
- Students: own purchase history (authenticated, tenant-scoped)

---

## 7. Functional requirements

### 7.1 Catalog & settings
- Catalog entry per (Tenant, Course, Plan): price, base currency. Snapshot at checkout — later catalog edits never rewrite Purchase history.
- Academy admins manage their own catalog; Platform admins manage opt-in + Commission Share per Academy.
- With go-live flag OFF: catalog manageable, Buy buttons hidden.

### 7.2 Checkout
- Authenticated Student → create Checkout Session (idempotent; one active pending session per (Student, Sellable) — reuse or expire-then-create).
- Hosted checkout; success/cancel return URLs; webhook-driven state (never trust the return URL alone).

### 7.3 Purchase state machine
`Created → Pending → Paid → (Refunded | Disputed→resolved)`, plus `Failed`, `Expired`. All transitions guarded and idempotent; out-of-order webhooks handled via status guards.

### 7.4 Webhooks
- Signature verification; dedupe table on PSP event ID (unique index); full event payload archived for audit.
- Handled: session completed / expired, refund events, dispute created / closed.

### 7.5 Ledger & Settlements
- Double-entry, append-only; DB-enforced balance invariant; no UPDATE/DELETE on entries (append-only enforced at the persistence layer).
- Entries recorded in the settlement currency; Purchase keeps presentment amount/currency + FX rate used.
- Settlements: manual record-only against Balance Owed (partial settlements allowed).

### 7.6 Refunds & Disputes
- Full refund via Platform dashboard → auto-revoke via saga.
- Disputes recorded from webhooks, surfaced in dashboard, resolved manually (outcome recorded).

---

## 8. Ledger accounting model

Accounts (settlement currency): `PSP Clearing` (asset, money in flight), `Platform Revenue` (income), `Academy Payable:<tenant>` (liability, per Academy), `PSP Fees` (expense), `Bank Account` (asset).

**Purchase captured** (gross G, PSP fee F reported at capture, commission c%):

```
Dr PSP Clearing                 G
    Cr Academy Payable:<t>      G × (1−c)
    Cr Platform Revenue         G × c
Dr PSP Fees                     F
    Cr PSP Clearing             F        → PSP Clearing nets G−F = PSP's reported net
```

**PSP payout to bank**: `Dr Bank Account / Cr PSP Clearing` — reconciles against PSP payout reports.
**Full refund**: reversal of the capture lines (PSP-retained fixed fee, if any, recorded to `PSP Fees`) + revocation event.
**Dispute lost**: same reversal + Dispute outcome recorded.
**Settlement**: `Dr Academy Payable:<t> / Cr Bank Account`.

Every entry carries an `AuditReference` linking Ledger ↔ Purchase ↔ webhook event ↔ PSP object ID.

---

## 9. Idempotency & failure handling (all four bars, D13)

1. **Every PSP API call** carries an idempotency key derived from a domain event ID (session creation keyed by PurchaseAttempt; refund keyed by RefundId).
2. **Webhook processing is effectively exactly-once** — PSP event-ID dedupe table with unique index; state-machine guards make replays no-ops.
3. **Checkout Session creation deduped** per (Student, Sellable) — one active pending session.
4. **State change + Ledger entries + outbox publish in one DB transaction** (module scaffold's `TransactionalBehavior`); downstream provisioning is idempotent and parks failures (`FailedProvisioningRecord`) instead of throwing.

---

## 10. Dashboard & analytics (MVP five)

1. **Revenue** by day / Academy / product — gross, Commission Share, net Academy share.
2. **Transaction list** with filters (status, Academy, product, date, currency) + drill-down to Purchase detail: state history, Ledger entries, webhook trail, PSP object IDs.
3. **Refund rate** (refunded ÷ paid) by Academy and product.
4. **Top products** leaderboard (units + revenue).
5. **Settlement position** per Academy: Balance Owed, settled to date, last Settlement.

Audience: Platform overview (all Academies) + Academy-scoped views (own data only, via existing IAM machinery). Students see their own purchase history in-account.

---

## 11. Acceptance criteria (MVP definition of done)

- [ ] Student buys an opted-in, catalogued Course Plan with a test-mode card → access identical to code redemption (Enrollment + principal assignment), granted exactly once, within seconds.
- [ ] Re-delivered / replayed webhooks cause no double grants, double Ledger entries, or double charges (verified by tests).
- [ ] Re-running provisioning is safe; failures park in `FailedProvisioningRecord` and never throw.
- [ ] Every Purchase is traceable end-to-end: Ledger entries (balanced) ↔ webhook events ↔ PSP object IDs, joined by `AuditReference`.
- [ ] Refund revokes access via the saga; Academy Balance Owed decreases accordingly.
- [ ] Disputes recorded and visible; resolution outcome recordable.
- [ ] Settlements recordable; derived Balance Owed matches Ledger sums; dashboard figures reconcile with the Ledger.
- [ ] Double-entry invariant enforced at the DB; entries are append-only (no UPDATE/DELETE path).
- [ ] Catalog + opt-in + commission configurable; Buy surfaces respect both.
- [ ] Only the Payments module references the PSP; Courses/Identity untouched except through existing facades; NetArchTest module-boundary rules pass.
- [ ] Go-live flag OFF by default; no live charges possible until enabled with live credentials.

---

## 12. Roadmap

**Phase 1 (MVP)** — suggested milestones:
1. Catalog, Academy settings, opt-in, permissions
2. Checkout sessions + webhook ingestion + Purchase state machine
3. Ledger + allocation + outbox Purchase Activation → Courses provisioning
4. Refunds + disputes (recording) + revocation integration
5. Settlements + dashboard + analytics projections
6. Architecture tests, idempotency tests, docs (ADRs, Permissions.md, swagger)

**Phase 2 (post-MVP, in priority order)**:
Go-live flip (config change) → partial refunds → PSP-native promotion codes → bridge model (academy-collects + commission invoicing) → Connect auto-payouts if Academy payout reachability opens → Blogs/Videos as sellables → subscriptions → custom discount engine → funnel/cohort analytics → Stripe Tax / MoR evaluation.

---

## Appendix A — Queued ADRs (write to `docs/adr/` when implementation starts)

**0002 — Platform is merchant of record; Academy shares settle off-provider via Ledger balances.**
Platform-as-seller with no PSP Connect accounts: the PSP collects 100% into the Platform account; each Purchase allocates Commission Share and Academy share as Ledger entries; Settlements are manual, record-only transfers. Chosen because Academies' end market is not reachable by PSP payouts; auto-payouts (Connect) deliberately deferred. A future reader will wonder why there is no Connect — this is why.

**0003 — Prices live in the Payments module, not on CoursePlan.**
The Price Catalog is owned by the Payments module; `CoursePlan` carries a principal and an access window but deliberately no price. Boundary decision: Pricing is a Payments concern; Courses never prices. Rejected the "price field on the plan" alternative to keep Payments a true bounded context.

**0004 — Payments module is PSP-agnostic; online go-live gated on a compliant PSP account.**
The module talks to an `IPaymentProvider` adapter; the Stripe implementation ships in test mode behind a go-live feature flag. Context: the founder is a Syrian resident; US/EU sanctions were lifted (2025–2026) but every PSP's own policy still bars Syria-resident/citizen principals. The module therefore ships complete and revenue-safe (access codes remain the live economy), with go-live as a configuration change once a legitimate PSP path exists (policy change or genuine relocation + entity formation). Constraint not visible in code — recorded here so nobody "fixes" the disabled checkout.

## Appendix B — Queued glossary additions

- **Sellable**: A Course Plan enabled for online selling — a Price Catalog entry plus the Academy's opt-in. _Avoid_: product, offering.

## Appendix C — Risks & open items

- **Test-mode access**: Stripe is IP-blocked from Syria even for site access; test-mode account creation may need to happen during genuine travel abroad (honest presence, not misrepresentation). Backend-to-PSP API calls run from the AWS-hosted app regardless.
- **Stage-1(b) screening**: even after relocation, a Syrian *citizen* may trip PSP screening ("citizen of" clauses). Engage sanctions-competent counsel before entity formation. This document is not legal advice.
- **Policy momentum**: PSP availability for Syria is changing quarter-to-quarter; recheck Stripe's global-availability page each quarter — the unlock may arrive earlier than expected.
- **FX/accounting**: settlement currency is fixed when the PSP account opens; catalog stores currency per entry so the base-currency choice is reversible until go-live.
