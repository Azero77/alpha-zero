# Payments Module — Requirements

**Status:** Synchronized 2026-11-18. Product decisions D1–D17 and Q1–Q58 are settled; implementation remains blocked until the feature architecture passes engineering review.

**Document ownership:** this file owns product scope and acceptance. [`database.md`](database.md) owns schema and data invariants. [`transaction_flows.md`](transaction_flows.md) owns provider-neutral workflows. [`stripe_payments_transaction_flows.md`](stripe_payments_transaction_flows.md) and [`sham_cash_payments_transaction_flows.md`](sham_cash_payments_transaction_flows.md) own provider wire contracts and deltas. [`design.md`](design.md) owns Clean Architecture, CQRS, and implementation sequencing.

---

## 1. Why this module exists

AlphaZero's current economy is offline: Libraries sell Access Codes and the Platform never handles that money. Payments adds the online economy: authenticated Students pay through an enabled provider, receive course access, Academies accrue their share, and every movement is recorded in an auditable double-entry Ledger.

### MVP outcomes

1. One-time online Purchases grant course access through the same Courses-owned capability used by Access Codes, with truthful source identity.
2. Stripe and ShamCash coexist behind capability-specific provider adapters. A provider exposes only operations it actually supports.
3. Each Purchase allocates the stored Commission Share between Platform Revenue and an Academy Payable account scoped to provider account and currency.
4. Refunds, disputes, unapplied receipts, settlements, and corrections append evidence-backed Ledger entries. Existing money history is never rewritten.
5. Platform and Academy dashboards expose provider-account/currency-scoped financial views without inventing cross-currency totals.
6. Checkout stays disabled per provider account until credentials, compliance, sandbox evidence, reconciliation, monitoring, and legal gates pass.

---

## 2. Compliance and release context

- Payment-provider policy, not application capability, is the current online-card blocker for a Syria-resident founder. The module ships provider-neutral and revenue-safe while Access Codes remain live.
- Provider accounts are Platform-owned, dedicated to AlphaZero, and isolated by provider and environment. Test events can never mutate live money.
- Stripe Live requires supported-account eligibility, valid credentials, compliance approval, and live sandbox evidence.
- ShamCash is an exact-transfer provider with a dedicated linked account managed on its own dashboard. AlphaZero monitors account health but does not link, renew, or unlink it in-app.
- Holding Academy funds and settling them manually requires qualified legal sign-off or a revised provider-held split-payment model before go-live. `TODOS.md` tracks this release gate.
- The Academy-collects bridge model, subscriptions, and Connect-style payouts are later decisions, not hidden MVP behavior.

This document records product and engineering constraints, not legal advice.

---

## 3. Scope

### In scope

- Payments-owned Product mirror and immutable Prices for Course Plans.
- Per-Academy selling opt-in, Commission Share, and optional self-service refund window.
- Provider registry, Platform Provider Accounts, and enabled currency routes.
- Stripe hosted Checkout with Adaptive Pricing: integration/settlement Price remains authoritative; customer-local presentment is recorded separately.
- ShamCash hosted exact invoices for SYP→SYP and USD→USD, with a 30-minute payment window.
- One pending Checkout Session per Student/Product, locking one Price and provider route.
- Immutable Provider Transactions and provider-scoped double-entry Ledger accounts.
- Partial refunds, student Refund Requests, provider-originated refunds, Stripe disputes, and per-refund access-revocation decisions.
- Unapplied Funds plus maker/checker review for partial, overpaid, duplicate, late, or unmatched receipts.
- Evidence-backed, maker/checker Settlements from the exact provider/currency payable account.
- Durable webhook ingest and processing, reconciliation, archival, audit, analytics, monitoring, and replay.
- Source-aware Courses grant/revoke commands shared by Library and Payments origins.

### Out of scope

- Recurring billing or subscriptions.
- Discounts, promotion codes, tax calculation, and invoice numbering.
- Stripe Connect or automatic Academy payouts.
- Blogs, standalone Videos, or non-Course-Plan Products.
- Guest checkout.
- Academy-collects bridge model.
- Cross-provider or cross-currency normalized totals.
- Provider customer/price mirrors until a subscription or saved-customer use case exists.
- Database partitioning or Ledger balance checkpoints before accepted benchmark thresholds fail.

---

## 4. Domain language

Canonical definitions live in `GLOSSARY.md`.

| Term | Meaning | Not this |
|---|---|---|
| Purchase | Confirmed online acquisition of course access funded by one provider movement | Library Sale, checkout attempt |
| Checkout Session | A provider-bound attempt that locks Student, Product, Price, and currency route | Purchase, cart |
| Provider Transaction | Immutable normalized external money movement | webhook delivery, Ledger entry |
| Refund Request | Student request reviewed by Platform; approval creates a Refund | money movement |
| Refund | Partial or full Purchase reversal: Pending reserves refundable amount, Succeeded confirms provider movement, Failed releases the reservation | request, dispute |
| Remaining Refundable | Gross minus pending/succeeded Refunds and Dispute Exposure currently withdrawn and not reinstated | settleable balance |
| Unapplied Funds | Liability for received money that cannot yet become a Purchase | failed payment |
| Payment Review Case | Evidence-backed maker/checker decision for exceptional money handling | direct status override |
| Settlement | Confirmed transfer reducing one Academy/provider/currency payable | provider payout |
| Provider Payout | Provider movement from its clearing balance to a Platform bank account | Academy Settlement |

---

## 5. Decision record

### 5.1 Product foundation (D1–D17)

| # | Decision |
|---|---|
| D1 | One-time Purchases only. |
| D2 | Course Plans are the only MVP sellables. |
| D3 | Platform is seller/merchant of record; no Connect accounts; Academy shares settle from Ledger balances after legal approval. |
| D4 | Default Commission Share is 25% Platform / 75% Academy, configurable by Platform admins and computed on settlement gross. Provider fees remain a Platform expense. |
| D5 | Platform admins control Academy selling opt-in and Commission Share. Academy admins control their own Prices. |
| D6 | Course access uses the Plan's access window and a shared, source-aware Courses grant capability. |
| D7 | **Superseded by Q31/Q39–Q41/Q51:** refunds may be partial; capability and review policy vary by provider. |
| D8 | Checkout is provider-hosted. |
| D9 | Checkout is authenticated-Student-only and tenant-scoped. |
| D10 | **Amended by Q45/Q47/Q48/Q54:** Prices are provider-neutral per currency; Stripe may localize presentment; ShamCash is exact currency only; analytics uses settlement partitions. |
| D11 | Products and Prices belong to Payments, not Courses. |
| D12 | Ledger is double-entry, append-only, immutable, and database-enforced. |
| D13 | Idempotency is required at provider call, delivery, movement, domain-result, and Ledger-effect boundaries. |
| D14 | Dashboard provides Platform and Academy views for revenue, transactions, reversals, products, and settlement position. |
| D15 | Selling is fail-closed: absent/disabled Academy settings or provider routes hide checkout. |
| D16 | Go-live is isolated per Provider Account and environment, not one global Stripe flag. |
| D17 | Academy-collects remains Phase 2. |

### 5.2 Refund and provider amendments (Q39–Q58)

Q1–Q38 remain ratified in [`database.md`](database.md#1-decision-ledger-q1q58-settled). These later decisions define current product behavior:

| Q | Decision |
|---|---|
| Q39 | Refund Requests are first-class for refund-capable providers; one may be open per Purchase. Approval chooses the amount and atomically creates a pending Refund. |
| Q40 | Access revocation is a decision per refund. Full refund and lost dispute always revoke; a partial refund revokes only when `RevokeAccess = true`. |
| Q41 | Remaining Refundable = settlement gross − pending/succeeded Refunds − Dispute Exposure currently withdrawn and not reinstated. Opening paperwork contributes zero; one formula drives approval, sum caps, and settlement holds. |
| Q42 | Provider identity is an immutable machine name such as `stripe` or `sham-cash`; code registration uses the same key. |
| Q43 | Provider Accounts represent actual Platform-owned accounts per provider/environment. Secrets stay in KeyVault. |
| Q44 | Provider assets, revenue, fees, unapplied funds, and Academy payables are partitioned by Provider Account and currency. Bank/Cash may be provider-neutral. |
| Q45 | Provider Currency Routes declare enabled integration/settlement currencies and presentment mode. Prices remain provider-neutral. |
| Q46 | One pending attempt per Student/Product locks one Price and route. Method switching waits for completion or expiry. |
| Q47 | Stripe Adaptive Pricing may change customer presentment; allocation and analytics use provider-reported settlement gross. |
| Q48 | ShamCash uses an exact hosted invoice, single-use payment note, `order_id` and idempotency key equal to CheckoutSessionId, and a 30-minute window. |
| Q49 | Provider Transaction identity, unique within a Provider Account, is the authority for whether external cash was already observed. |
| Q50 | Partial, excess, duplicate, late, or unmatched incoming money posts to Unapplied Funds and creates no Purchase or access until approved review. |
| Q51 | Provider aftercare follows capability: Stripe supports ordinary refunds/disputes; ShamCash supports neither and routes exceptions through support review. |
| Q52 | Manual money handling requires evidence and different maker/checker actors. Approval never rewrites original events, movements, Purchases, or Ledger entries. |
| Q53 | Settlement reduces one exact Academy/provider/currency payable only after confirmed transfer. Stripe uses hold/reserve policy; ShamCash requires successful daily reconciliation then no rolling reserve. |
| Q54 | Analytics is partitioned by Provider Account and settlement currency. MVP exposes no normalized global money total. |
| Q55 | Provider degradation blocks new checkout only. Existing ingest, reconciliation, review, and history continue. |
| Q56 | Providers and Test/Live environments are independently gated. No test event may affect live financial state. |
| Q57 | Webhooks are notifications, not sole truth. Provider reports, invoices, transaction histories, and balances reconcile every movement. |
| Q58 | MVP has no provider customer or price mirrors. Stripe uses inline price data; ShamCash invoice amount/currency comes from the locked Price. |

---

## 6. Architecture contract

### 6.1 Layer and transaction rules

- HTTP endpoints, webhook consumers, bus consumers, and workers map input into public Application commands or queries. They contain no financial policy.
- Public provider workflows are non-transactional orchestrator commands. Idempotent internal `ITransactionalCommand` phases commit intent and result around provider network I/O. No network call runs under a database transaction or row lock.
- Domain models are separate from persistence models. Correctness never depends on EF ChangeTracker state.
- Infrastructure uses explicit adds, targeted SQL, or `ExecuteUpdate`; provider wire models never cross into Domain.
- Capability-specific ports replace one broad provider abstraction: checkout, refund, event normalization, and reconciliation are resolved from an immutable-name registry.
- One hardened database posting function is the only runtime path that inserts `LedgerEntries` and `LedgerLines`.

### 6.2 Access integration

```text
Library redemption ─┐
                    ├─> Courses Application: GrantCourseAccess(SourceType, SourceId, ...)
Payments activation ┘

Library revocation ─┐
                    ├─> Courses Application: RevokeCourseAccess(SourceType, SourceId, ...)
Payments reversal ──┘
```

Courses owns grant/revoke behavior, failure parking, enrollment, principal assignment, and idempotency. Library and Payments consumers are thin origins. Records and uniqueness use `(TenantId, SourceType, SourceId)`; a PurchaseId is never stored in an `AccessCodeId` field.

### 6.3 Provider matrix

| Capability | Stripe | ShamCash |
|---|---|---|
| Hosted checkout | Checkout Session | Hosted invoice |
| Presentment | Adaptive | Exact SYP/USD |
| Ordinary refund | Yes, partial/full | No |
| Dispute events | Yes | No |
| Movement truth | balance transactions/reports | transaction IDs/history/balance |
| Settlement source | normally Platform bank after provider payout | ShamCash Wallet or configured same-currency source |
| Release evidence | signed fixtures + secret-gated test smoke | signed fixtures + secret-gated sandbox smoke |

---

## 7. Functional requirements

### 7.1 Catalog, settings, and routing

- Courses lifecycle events maintain a versioned Payments Product mirror.
- A Price is immutable. Changing amount archives the old Price and inserts another.
- Checkout eligibility requires an active Product, active Price, Academy opt-in, no active enrollment, and an active compatible Provider Currency Route.
- If several providers support the Price currency, the Student chooses before one pending session locks the route.

### 7.2 Checkout and purchase

- Creating a session is idempotent by CheckoutSessionId and one-pending-session uniqueness.
- Provider timeout or ambiguity remains `Pending`; only definitive rejection/expiry makes the attempt terminal.
- Success/cancel return URLs never activate access. Verified provider movement does.
- A valid exact movement creates one Purchase, posts one allocation, publishes one activation event through the EF outbox, and completes the session atomically.

### 7.3 Webhooks and reconciliation

- Signature verification happens against raw bytes before any database write.
- Ingest commits delivery, payload, and internal processing message atomically through the Payments EF outbox, then responds 2xx.
- Delivery dedupe is (`ProviderName`, `ExternalEventId`); movement dedupe is (`ProviderAccountId`, `ExternalTransactionId`).
- A sweeper repairs stale/non-terminal events; it is not the normal handoff.
- Reconciliation persists runs and findings for Ledger consistency, provider movements/balances, provider health, mirror drift, stale sessions, Unapplied Funds, and negative payables.

### 7.4 Refunds, disputes, and reviews

- Stripe Students may request refunds inside the configured Academy window. Approval chooses amount, reason, and revocation behavior.
- Refund creation locks the Purchase and enforces the Q41 Remaining Refundable formula. Ambiguous provider outcomes stay pending.
- Provider-originated Stripe refunds are ingested even without a prior local request.
- Stripe dispute cash is booked when funds are withdrawn/reinstated; closing the dispute changes paperwork and revocation state only.
- ShamCash ordinary refund/dispute routes fail closed and direct the Student to support.
- Every review case requires immutable evidence; money leaves or is applied only after a different checker approves and movement evidence exists.

### 7.5 Ledger and settlement

- The posting function atomically inserts a header and all balanced lines inside the caller's transaction; direct runtime inserts are denied.
- Every line uses the entry's currency, tenant, Provider Account partition, and ISO minor-unit scale. Deferred checks verify at least two lines and balanced debits/credits.
- Balance Owed and Settleable Balance are derived per payable account. No provider or currency netting is implicit.
- Settlement locks the exact payable, applies provider risk/reconciliation gates, and records only a completed transfer with destination snapshot, evidence, and unique external reference.

### 7.6 Archive and privacy

- Hot webhook payload JSON is retained until a cold object upload is verified.
- The database retains a manifest with EventId, object key, content hash, archive/redaction timestamps, and lifecycle state.
- Replay reads hot payload or verifies/reads the cold object through one archive reader.
- PII redaction is audited; immutable financial identifiers and money facts remain.

---

## 8. Accounting shapes

Every account below is scoped to the relevant Provider Account and currency unless marked provider-neutral.

**Capture G with fee F and Commission Share c**

```text
Dr Provider Asset              G
    Cr Academy Payable         G − ROUND(G × c)
    Cr Platform Revenue        ROUND(G × c)
Dr Provider Fees               F
    Cr Provider Asset          F
```

**Refund amount R** reverses the stored split pro rata; provider fees reverse only when provider movement truth says they did.

**Unapplied receipt A**

```text
Dr Provider Asset              A
    Cr Unapplied Funds         A
```

**Settlement S**

```text
Dr Academy Payable             S
    Cr same-currency source    S
```

[`database.md`](database.md) owns exact reference sets, account roles, constraints, and worked examples.

---

## 9. Authorization

Canonical strings and actor scope are in `docs/Permissions.md`.

- Platform finance controls Academy settings, provider configuration, refund decisions, review approval, adjustments, settlements, platform analytics, and reconciliation.
- Academy operators manage their own catalog, view their scoped analytics, and may open evidence-backed review cases but never approve their own cases.
- Students create checkout sessions, view their own Purchases, and request refunds only where the provider and Academy policy allow.

Authorization does not replace tenant/provider/currency checks inside transactional handlers.

---

## 10. Analytics

1. Revenue by day, Academy, Product, Provider Account, and settlement currency.
2. Transaction list and drill-down through Purchase, Provider Transaction, Ledger entries, webhook trail, and audit trail.
3. Refund/reversal rate with explicit provider capability and cohort semantics.
4. Top Products within one Provider Account/currency partition.
5. Settlement position per Academy payable: Ledger balance, holds, reserve, open refund exposure, reconciliation gate, settled-to-date, and last Settlement.

UI sections may display several partitions but must not sum them into one money amount without an approved FX/reporting model.

---

## 11. Acceptance criteria

- [ ] Stripe and ShamCash provider names, accounts, external IDs, events, and movements cannot collide.
- [ ] Two tabs/retries create one pending Checkout Session and at most one Purchase/access grant.
- [ ] Provider timeout preserves durable intent and retry uses the same idempotency key.
- [ ] Ingest commit plus process crash still dispatches through the EF outbox; duplicate dispatch is a no-op; sweeper repair works.
- [ ] Direct runtime inserts into Ledger tables fail; the posting function accepts balanced entries and rejects malformed, cross-tenant, cross-provider, mixed-currency, sub-minor-unit, or replayed entries.
- [ ] Stripe Adaptive Pricing stores customer presentment while Ledger allocation and analytics use settlement truth.
- [ ] ShamCash exact in-window receipt creates one Purchase; partial, overpaid, duplicate, late, or unmatched receipts create no Purchase and balance through Unapplied Funds.
- [ ] Concurrent refunds/disputes cannot exceed Remaining Refundable or duplicate a reversal.
- [ ] Full refund, lost dispute, and flagged partial refund revoke access exactly once; an unflagged partial refund does not.
- [ ] Library and Payments access origins have parity, truthful source IDs, collision safety, retry, and idempotent revocation.
- [ ] Same-actor maker/checker, missing evidence, stale balance, cross-provider settlement, and unresolved ShamCash reconciliation are rejected before booking.
- [ ] Disabling one provider route blocks only new checkout on that route; ingest, history, and other providers continue.
- [ ] Archive upload failure retains hot JSON; hash mismatch blocks clearing; verified cold restore and PII erasure are tested.
- [ ] Analytics and settlements never mix Provider Accounts or currencies.
- [ ] PR tests use deterministic signed provider fixtures. A provider slice is not release-ready until its secret-gated sandbox smoke passes; missing credentials reports **BLOCKED**, not passed.
- [ ] Data-shaped PostgreSQL benchmarks record p50/p95 ingest, process, re-drive, Ledger SUM, table bytes, and index bytes. Partitioning/checkpoints remain absent only while documented thresholds pass.
- [ ] Go-live defaults off and requires credentials, compliance, reconciliation, monitoring, restore rehearsal, sandbox proof, and legal sign-off.

---

## 12. Delivery sequence

The full scope ships as independently verifiable vertical slices:

1. **Plans and architecture:** synchronize Q39–Q58, database contracts, shared flows, provider deltas, permissions, glossary, and `design.md`; pass engineering review.
2. **Financial kernel:** provider registry/config, Product/Price, Checkout intent, Provider Transactions, hardened Ledger posting, outbox, and core tests.
3. **Source-aware access:** generalize Courses grant/revoke once; preserve Library behavior and prove Payments parity.
4. **ShamCash slice:** exact invoice, Unapplied Funds, reconciliation, maker/checker review, and sandbox proof.
5. **Stripe slice:** Adaptive Pricing checkout, refund/dispute/payout behavior, and test-mode proof.
6. **Aftercare and operations:** settlement, analytics, archive, reconciliation, monitoring, benchmarks, restore and runbooks.

Financial kernel and source-aware access may run in parallel after architecture approval. Provider slices start only after both merge.

---

## Appendix A — queued ADRs

Write these before the corresponding implementation slice:

1. **0002 — Platform is merchant of record; Academy shares settle from provider/currency Ledger payables.** Records the no-Connect choice, provider-specific settlement sources, and legal gate.
2. **0003 — Products and immutable Prices belong to Payments.** Records the event-fed Courses mirror and no cross-schema foreign keys.
3. **0004 — Provider capabilities and accounts gate online checkout independently.** Records the immutable-name registry, capability-specific ports, Test/Live isolation, and why enabling a provider is configuration plus evidence, not a global Stripe switch.
4. **0005–0014** are listed in [`database.md`](database.md#10-adr-worthy-flags-draft-titles), including tenancy, session/purchase split, DB-enforced Ledger integrity, tax exclusion, partial refunds, provider partitioning, Unapplied Funds, source-aware access, durable webhook handoff, and archive manifests.

## Appendix B — open external gates

- Stripe supported-account eligibility and test/live credentials.
- ShamCash sandbox credentials and published contract verification.
- Qualified legal sign-off for the merchant-of-record/manual-settlement model.
- Product volume forecast used to generate the 10× data-shaped benchmark fixture.
