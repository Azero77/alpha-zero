# Payments — Feature Architecture

**Status:** Slice 0 architecture cleared 2026-11-20. This is the implementation contract for later slices; external legal, credential, and sandbox gates remain with their owning slices.

**Domain revision (2026-11-20):** §4 defines behavioral Domain Models independently from persistence DataModels. Refund, Dispute, and Purchase remain separate aggregate roots because their provider-driven lifecycles are independent; Q41 is an explicit cross-aggregate Domain policy evaluated from immutable facts while Infrastructure serializes the decision with the Purchase lock. Domain events remain inside Payments and are translated to the integration events defined in `transaction_flows.md` before crossing into Courses.

**Companions:** [`requirement.md`](requirement.md) owns product scope and Q39–Q58 behavior. [`database.md`](database.md) owns schema and data invariants. [`transaction_flows.md`](transaction_flows.md) owns shared workflow sequencing. [`stripe_payments_transaction_flows.md`](stripe_payments_transaction_flows.md) and [`sham_cash_payments_transaction_flows.md`](sham_cash_payments_transaction_flows.md) own provider wire contracts and deltas. Repository-root [`GLOSSARY.md`](../../GLOSSARY.md) owns terminology. Repository-root `DESIGN.md` remains the product visual design system and is not changed by this feature.

---

## 1. Architecture goals

1. Keep financial policy in a provider-neutral Domain.
2. Keep use-case sequencing and authorization in Application.
3. Keep provider SDK/HTTP, EF data models, SQL, migrations, object storage, and worker mechanics in Infrastructure.
4. Keep endpoints and consumers as input/output mapping only.
5. Make illegal provider operations uncallable by resolving capability-specific ports.
6. Make every external call recoverable through committed intent and deterministic identity.
7. Make one database function the only runtime Ledger write path.
8. Generalize Courses access once; never build a second Payments-specific enrollment pipeline.
9. Ship the approved end state in independently testable vertical slices.

---

## 2. Dependency and runtime shape

```mermaid
flowchart TD
    subgraph Origins[Presentation and consumers]
        HTTP[FastEndpoints]
        WH[Webhook endpoints]
        BUS[MassTransit consumers]
        WORK[Workers]
    end

    subgraph App[Application]
        CMD[Public commands and queries]
        ORCH[Provider orchestrators]
        PHASE[Internal transactional phases]
        PORTS[Capability and persistence ports]
    end

    subgraph Domain[Domain]
        AGG[Aggregates and value objects]
        POL[Pure financial policies]
        POST[Ledger postings]
    end

    subgraph Infra[Infrastructure]
        ADAPT[Stripe / ShamCash adapters]
        PERSIST[EF DataModels + explicit Add / targeted update / SQL]
        LEDGER[Hardened Ledger writer]
        OUTBOX[MassTransit EF outbox]
        ARCHIVE[Object archive]
    end

    HTTP --> CMD
    WH --> CMD
    BUS --> CMD
    WORK --> CMD
    CMD --> ORCH
    CMD --> PHASE
    ORCH --> PHASE
    ORCH --> PORTS
    PHASE --> AGG
    PHASE --> POL
    PHASE --> PORTS
    POL --> POST
    PORTS -. implemented by .-> ADAPT
    PORTS -. implemented by .-> PERSIST
    PORTS -. implemented by .-> LEDGER
    PERSIST --> OUTBOX
    PERSIST --> ARCHIVE
```

Compile-time direction is inward: Presentation → Application → Domain. Infrastructure implements Application ports and references Domain only for mapped values/contracts. Domain references no EF, MediatR, MassTransit, provider SDK, HTTP, object storage, or module facade.

### Runtime rule

Every origin calls a public Application command/query. It may not call Domain services, persistence ports, DbContext, provider adapters, or internal phase commands directly. Architecture tests enforce this.

### Domain ownership rule

Financial policy — status transitions, the Remaining Refundable and Settleable Balance formulas, posting balance, and their events — lives only in Domain (§4). An Application phase fetches facts under lock, invokes the required Domain policy and aggregate methods, and persists their outcomes via tripwired writes; it never re-implements a transition or invariant as a handler `if`. If a rule is deterministic and I/O-free, it is a Domain method or Domain service. Architecture tests fail on anemic aggregates and on business rules found in Application handlers.

Aggregate boundaries are not persistence boundaries. A Domain aggregate may be reconstituted from several EF DataModels, and several Domain aggregates may participate in one short database transaction when an approved cross-aggregate invariant requires it. DataModels mirror storage; Domain Models protect behavior. PostgreSQL constraints remain the race-safe final guard rather than a substitute for Domain policy.

---

## 3. Module layout

Keep the existing projects and add feature folders by capability, not table:

```text
Modules/Payments/
  Domain/
    Catalog/
      Aggregates/           # Product, Price
      Events/               # ProductMirrorUpdated, PriceCreated, PriceArchived
    Settings/
      Aggregates/           # AcademyPaymentSettings, ProviderConfiguration
      Events/               # AcademyPaymentSettingsUpdated, ProviderAccount/RouteConfigured
    Checkout/
      Aggregates/           # CheckoutSession (+ state)
      Events/               # CheckoutSessionCreated/Completed/Expired
      Services/             # CheckoutEligibilityPolicy
    Purchases/
      Aggregates/           # Purchase (+ state)
      Events/               # PurchasePaid, PurchasePartiallyRefunded, PurchaseFullyRefunded
      Services/             # RefundabilityPolicy
    Aftercare/
      Aggregates/           # RefundRequest, Refund, Dispute (+ states)
      Events/               # RefundRequested/Decided/Created/Succeeded/Failed, DisputeOpened/Resolved
    Finance/
      Aggregates/           # PaymentReviewCase (+ state)
      Records/              # validated factories: ProviderTransaction, Settlement, audit records
      Events/               # SettlementRecorded, UnappliedFundsReceived, PaymentReview lifecycle
      Services/             # SettlementPolicy
    Ledger/
      ValueObjects/         # Money, LedgerPosting
      Services/             # LedgerPostingFactory
    Providers/
      ValueObjects/         # ProviderName, ProviderAccountRef, ProviderCurrencyRoute, PaymentMethod, ProviderTransactionIdentity
  Application/
    Catalog/
    Checkout/
    Webhooks/
    Aftercare/
    Finance/
    Reconciliation/
    Reporting/
    Ports/
  Infrastructure/
    Persistence/
      Models/
      Configurations/
      Mappers/
      Stores/
      Migrations/
    Ledger/
    Providers/Stripe/
    Providers/ShamCash/
    Webhooks/
    Archive/
    Workers/
  IntegrationEvents/
  Presentation/
    Endpoints/
    Consumers/
```

Do not create one repository/service pair per table. A store may own a coherent workflow boundary, such as checkout intent or finance review, and may update several persistence models atomically.

Within each Domain capability folder, keep each Domain Model with its behavior and put events, policies, and value objects beside it. Do not infer this shape from EF DataModels: a mapper/store may compose multiple rows into one Domain Model or persist one Domain outcome across multiple rows. No Domain project may reference EF, MediatR, MassTransit, a provider SDK, HTTP, or a module facade. No money DataModel implements `ISoftDeletable`, and the Payments `AppDbContext` does not register the `SoftDeleteInterceptor` (§7.1).

---

## 4. Domain model

Domain Models protect decisions; persistence DataModels model rows. They are deliberately separate. Infrastructure may map one DataModel to one Domain Model, compose several DataModels into one aggregate/policy snapshot, or persist one Domain outcome across several DataModels. **Financial rules live in Domain, while locking and persistence mechanics stay outside it.**

### 4.1 Aggregates

The roots below are chosen for independent lifecycle and identity, not because similarly named tables exist. Refunds and Disputes receive provider events independently from Purchase, so they remain separate roots. Q41 is intentionally cross-aggregate and is described in §4.2.

| Aggregate | Identity and lifecycle | Commands / behaviors | Internally owned invariants | Cross-aggregate participation | Domain events | Facts required for `Reconstitute` |
|---|---|---|---|---|---|---|
| `Product` | `ProductId`; mirrored `Unavailable ↔ Available` by increasing source version | `ApplyMirrorUpdate(sourceVersion, availability, …)` | only a strictly newer version applies; no mutable purchasability flag | Checkout derives Sellable from source availability + Price + Academy settings + route | `ProductMirrorUpdatedDomainEvent` | id, tenant/item/parent IDs, ARN, title, source availability/version, timestamps |
| `Price` | `PriceId`; `Active → Archived` | `Create`, `Archive` | positive minor-unit Money; Product/currency/amount immutable; no unarchive | Domain precondition checks no active sibling; DB partial unique is race guard | `PriceCreatedDomainEvent`, `PriceArchivedDomainEvent` | id, tenant/product IDs, Money, active/archive facts, creator/time |
| `AcademyPaymentSettings` | `TenantId`; absent or configured, then policy revisions | `CreatePolicy`, `UpdatePolicy` | commission/window ranges; absent means selling disabled | supplies checkout, refund-window, and settlement inputs | `AcademyPaymentSettingsUpdatedDomainEvent` | id/tenant, selling flag, commission, refund window, actor/time |
| `ProviderConfiguration` | `ProviderAccountId` root with route identities; account `Active/Degraded/Disabled`, routes enabled/disabled | `ConfigureAccount`, `SetAccountStatus`, `ConfigureCurrencyRoute`, `DisableCurrencyRoute` | immutable Provider name/environment/account identity; currency/presentment/capability/risk coherence | projects Payment Methods; route enabling requires adapter and structural Ledger accounts | `ProviderAccountConfiguredDomainEvent`, `ProviderAccountStatusChangedDomainEvent`, `ProviderCurrencyRouteConfiguredDomainEvent`, `ProviderCurrencyRouteDisabledDomainEvent` | account identity/status/secret references/times plus route IDs, currencies, modes, enabled/risk facts |
| `CheckoutSession` | `CheckoutSessionId`; `Pending → Completed/Expired`, `Expired → Completed` for valid late money | `Create`, `StampProviderSession`, `Expire`, `CompleteFromMovement` | locked Student/Product/Price/route immutable; hosted creation never completes payment; ambiguous create stays Pending | DB guards one Pending Student/Product; exact movement creates Purchase atomically | `CheckoutSessionCreatedDomainEvent`, `CheckoutSessionExpiredDomainEvent`, `CheckoutSessionCompletedDomainEvent` | id, tenant/user/product/price/route IDs, external ID, status and all timestamps |
| `Purchase` | `PurchaseId`; born `Paid`; `Paid → PartiallyRefunded \| Refunded`, `PartiallyRefunded → Refunded` | `CreateFromCapture`, `RecordConfirmedRefund`, `RecordLostDispute` | immutable capture/split facts; only confirmed reversals change status; a full first reversal goes directly to Refunded; confirmation does not reserve Q41 | Purchase row lock serializes Q41 snapshots; Ledger/outbox persist atomically | `PurchasePaidDomainEvent`, `PurchasePartiallyRefundedDomainEvent`, `PurchaseFullyRefundedDomainEvent` | all capture snapshot fields, refs, status/timestamps, confirmed reversal total/facts composed by mapper |
| `RefundRequest` | `RefundRequestId`; `Requested → Approved/Rejected/Cancelled` | `Create`, `Approve`, `Reject`, `Cancel` | requester/tenant/Purchase immutable; decision fields coherent; no money movement | one-open DB guard; approval follows Q41 and creates Pending Refund atomically | `RefundRequestedDomainEvent`, `RefundRequestDecidedDomainEvent` | id, purchase/tenant/requester, reason/currency, status, decision/refund refs, timestamps |
| `Refund` | `RefundId`; `Pending → Succeeded/Failed` | `CreateFromApprovedRequest`, `CreateFromProviderNotice`, `CreateFromSupportApproval`, `Succeed`, `Fail` | source, amount, currency, reason and revoke decision immutable; Pending reserves Q41; confirmation consumes reservation | policy/lock controls local creation; succeeded movement drives Purchase, Ledger and possible revocation | `RefundCreatedDomainEvent`, `RefundSucceededDomainEvent`, `RefundFailedDomainEvent` | id, purchase/tenant/provider/review/movement refs, source/actor/reason/revoke/Money, external ID/fee, status/times |
| `Dispute` | `DisputeId`; `Open → Won/Lost`, with independent withdrawal/reinstatement facts | `OpenFromProvider`, `RecordFundsWithdrawn`, `RecordFundsReinstated`, `ResolveWon`, `ResolveLost` | opening is paperwork only; each movement identity is idempotent; close posts no money | composed movement facts supply Q41 exposure; withdrawal/reinstatement write Ledger; Lost drives Purchase/revocation | `DisputeOpenedDomainEvent`, `DisputeFundsWithdrawnDomainEvent`, `DisputeFundsReinstatedDomainEvent`, `DisputeResolvedDomainEvent` | dispute row facts plus composed withdrawal/reinstatement Provider Transaction and Ledger references |
| `PaymentReviewCase` | `PaymentReviewCaseId`; `Open → Approved/Rejected`, `Approved → Resolved` | `Open`, `Approve`, `Reject`, `Resolve` | immutable evidence and typed amount/currency intent; checker differs from maker; approval alone moves no money | ProviderReversal approval creates Pending Refund; resolution may persist movement/Ledger/Settlement atomically | `PaymentReviewOpenedDomainEvent`, `PaymentReviewDecidedDomainEvent`, `PaymentReviewResolvedDomainEvent` | case refs/type/status, amount/currency, type-specific intent, evidence, maker/checker, resolution/times |

Domain types expose no EF navigation or public setters. Hydration uses `Reconstitute(...)` factories. A simple ladder uses guarded methods; use the Library State pattern only where it makes a genuinely complex transition matrix easier to read. Every persisted transition records its expected previous status for Infrastructure's conditional update tripwire.

### 4.2 Cross-aggregate Q41 decision

`RefundabilityPolicy` owns Q41; Application owns sequencing; Infrastructure owns locking. Pending Refund creation/reservation is the only local point that checks the cap:

```csharp
// Transactional approval phase — shape only.
lock Purchase row;
snapshot = store.LoadRefundabilitySnapshot(purchaseId);
decision = RefundabilityPolicy.Decide(snapshot, requestedAmount);
request.Approve(decision, refundId, now);
refund = Refund.CreateFromApprovedRequest(decision, ...); // Pending now reserves the amount
store.Persist(request, refund);
```

Confirmation does **not** recompute Q41 against the already-reserved Pending Refund. It validates the Refund/movement identity, transitions Refund to Succeeded, and passes immutable confirmed-reversal facts to Purchase so Purchase can update its status. A Failed Refund releases the reservation because it drops out of the next policy snapshot. Approved support cases create Pending Refunds only after Q41 admits the local decision. An accepted provider-originated notice creates its Pending Refund as external truth even if it raced local capacity; negative Remaining Refundable creates a Critical finding and blocks new local reservations. If the first observation is already-confirmed money, the same transaction creates and succeeds the Refund and books the movement rather than rejecting external cash.

The snapshot contains Purchase settlement gross, Pending/Succeeded Refund amounts, and dispute amounts currently withdrawn and not reinstated, whether the paperwork is Open or finally Lost. Merely opening or closing a Dispute contributes no exposure by status alone. PostgreSQL triggers and unique constraints are the final concurrency guard; they do not contain a second version of the formula.

### 4.3 Domain and integration events

Domain events are private Payments facts raised by aggregate methods. Integration events are Payments-owned public contracts written through the EF outbox after all required identifiers, including the backing LedgerEntry audit reference, are known. Courses consumes only integration events.

| Area | Events |
|---|---|
| Catalog | `ProductMirrorUpdatedDomainEvent`, `PriceCreatedDomainEvent`, `PriceArchivedDomainEvent` |
| Settings | `AcademyPaymentSettingsUpdatedDomainEvent`, `ProviderAccountConfiguredDomainEvent`, `ProviderCurrencyRouteConfiguredDomainEvent`, `ProviderCurrencyRouteDisabledDomainEvent` |
| Checkout | `CheckoutSessionCreatedDomainEvent`, `CheckoutSessionCompletedDomainEvent`, `CheckoutSessionExpiredDomainEvent` |
| Purchases | `PurchasePaidDomainEvent`, `PurchasePartiallyRefundedDomainEvent`, `PurchaseFullyRefundedDomainEvent` |
| Aftercare | `RefundRequestedDomainEvent`, `RefundRequestDecidedDomainEvent`, `RefundCreatedDomainEvent`, `RefundSucceededDomainEvent`, `RefundFailedDomainEvent`, `DisputeOpenedDomainEvent`, `DisputeFundsWithdrawnDomainEvent`, `DisputeFundsReinstatedDomainEvent`, `DisputeResolvedDomainEvent` |
| Finance | `SettlementRecordedDomainEvent`, `UnappliedFundsReceivedDomainEvent`, `PaymentReviewOpenedDomainEvent`, `PaymentReviewDecidedDomainEvent`, `PaymentReviewResolvedDomainEvent` |

| Business fact | Private Domain event | Ledger requirement | Public integration event | Consumer |
|---|---|---|---|---|
| Confirmed Capture creates Purchase | `PurchasePaidDomainEvent` | `Capture`/`ApplyUnappliedFunds` posting must succeed; returned LedgerEntry ID is `AuditReference` | `PurchaseActivatedIntegrationEvent` | Courses grant consumer |
| Succeeded unflagged partial Refund | `RefundSucceededDomainEvent`, `PurchasePartiallyRefundedDomainEvent` | one `RefundReversal` | none | Payments only |
| Succeeded flagged partial Refund | same Refund/Purchase events | one `RefundReversal`; revocation AuditReference is the resulting Payments audit row | `PurchaseAccessRevocationRequestedIntegrationEvent(CauseType=Refund)` | Courses revoke consumer |
| Purchase becomes fully Refunded | `RefundSucceededDomainEvent`, `PurchaseFullyRefundedDomainEvent` | one backing `RefundReversal`; revocation AuditReference is the resulting Payments audit row | same revocation contract | Courses revoke consumer |
| Dispute opens | `DisputeOpenedDomainEvent` | none | none | Payments only |
| Dispute funds withdrawn | `DisputeFundsWithdrawnDomainEvent` | one `DisputeLoss` | none | Payments only |
| Dispute funds reinstated | `DisputeFundsReinstatedDomainEvent` | one `DisputeWon` | none | Payments only |
| Dispute resolves Lost | `DisputeResolvedDomainEvent(Lost)` | no posting at close; revocation AuditReference is the Dispute resolution Payments audit row | `PurchaseAccessRevocationRequestedIntegrationEvent(CauseType=Dispute)` | Courses revoke consumer |
| Dispute resolves Won | `DisputeResolvedDomainEvent(Won)` | no posting at close | none | Payments only |
| Unapplied receipt observed | `UnappliedFundsReceivedDomainEvent` | one `UnappliedReceipt` | none | Payments only |
| Settlement transfer confirmed | `SettlementRecordedDomainEvent` | one `Settlement` posting | none | Payments only |

No Domain event handler calls Courses. Application translates the fact and writes the public event to the outbox in the same transaction as Domain state, Ledger posting, and audit. Activation `AuditReference` is always the final funding LedgerEntry ID. Revocation `AuditReference` is always the cause's `PaymentsAuditLogs.Id`; for Refund causes that audit row is created only after `ILedgerWriter` succeeds and records the reversal ID. There is no `LedgerPostingPreparedDomainEvent`: constructing a `LedgerPosting` is a synchronous return value with no in-process consumer. Posting success is recorded as audit/operational data only after `ILedgerWriter` returns the LedgerEntry ID; no event named “posted” is raised before success. A Lost close revokes immediately using its immutable resolution audit row when withdrawal has not yet arrived; later movement processing updates exposure/Purchase status without duplicating the Courses effect. New Domain event constructors receive an explicit `IClock` timestamp; Payments Domain does not import MediatR directly despite Shared's existing transitive `IDomainEvent` debt.

### 4.4 Value objects and policies

Value objects exist only where a multi-field invariant needs a type; IDs stay primitive `Guid` per Q2.

| Value object | Invariant it enforces |
|---|---|
| `Money(Amount, Currency)` | amount positive/non-negative by use; ISO 4217 currency; minor-unit granularity from the currency exponent; no cross-currency arithmetic |
| `ProviderName` | immutable machine slug; only registered names constructible |
| `ProviderAccountRef` | account id + provider + environment travel together |
| `ProviderCurrencyRoute` | account + integration/settlement currencies + presentment mode + risk policy travel together |
| `PaymentMethod` | Student-facing eligible checkout method derived from a Provider Currency Route |
| `ProviderTransactionIdentity` | `(ProviderAccountId, ExternalTransactionId)` is the cash dedupe key |
| `LedgerPosting` | one header + ≥2 balanced lines, all same currency, exact typed references; constructible only through `LedgerPostingFactory` |
| `AccessSource` | shape owned by Courses; shared contracts carry `(SourceType, SourceId)` |

| Service | Inputs | Output / responsibility |
|---|---|---|
| `CheckoutEligibilityPolicy` | Product, Price, Academy settings, Provider Currency Route, enrollment/access fact | eligible `PaymentMethod` values or explicit reason |
| `RefundabilityPolicy` | Purchase gross, Pending/Succeeded Refunds, currently withdrawn/unreinstated dispute exposure | Remaining Refundable and reservation decision (Q41) |
| `SettlementPolicy` | payable balance, Pending Refund Academy-payable legs, open-request exposure, route risk, reconciliation evidence | Settleable Balance (Q32/Q53), pre-transfer rejection reasons, and a post-transfer stale-intent finding; called under the payable lock at approval, preflight, and confirmation |
| `LedgerPostingFactory` | normalized movement plus stored purchase/split/review facts | balanced, minor-unit-safe `LedgerPosting` for each entry type |

These policies are deterministic and I/O-free. Time, provider reads, authorization, locks, `ILedgerWriter`, and persistence stay in Application/Infrastructure.

### 4.5 Immutable financial records

`ProviderTransaction`, `Settlement`, `LedgerEntry`, `LedgerLine`, and audit/reconciliation records are immutable validated Domain records, not mutable aggregates and not EF DataModels. Their factories validate identity shape, money granularity, and typed references. Application records success/audit events only after the corresponding Infrastructure operation succeeds. No append-only DataModel implements `ISoftDeletable`, and no generic repository is created for it.

### 4.6 Domain ↔ persistence mapping

For simple one-to-one shapes, Shared's actual `IDataModelMapper<TDomainModel, TDataModel>` contract supplies `ToDomain(TDataModel)`, `ToData(TDomainModel)`, and `ApplyChanges(TDomainModel, existingDataModel)`; its Domain type must derive from Shared `Entity` and its DataModel is a class. Composite shapes do not pretend to implement that two-type interface: an Infrastructure capability mapper consumes a persistence projection assembled from several DataModels and calls one or more `Reconstitute(...)` factories. Stores own explicit Add/targeted Update instructions and tripwire SQL; mappers do not. Domain assemblies never reference EF types, and query DTOs bypass Domain (`AsNoTracking` projections, §5.4).

---

## 5. Application design

### 5.1 Command classes

- **Public local command:** `ICommand<T>, ITransactionalCommand`. The existing `TransactionalBehavior` wraps the whole handler.
- **Public provider orchestrator:** `ICommand<T>` only. It dispatches internal transactional phase commands around provider I/O.
- **Internal phase:** `ICommand<T>, ITransactionalCommand`, internal visibility where project boundaries allow, and never called by Presentation/Consumers.
- **Query:** `IQuery<T>`/repository convention used by the codebase; explicit tenant/provider/currency scope is mandatory for unfiltered money tables.

The current `TransactionalBehavior` opens a transaction for every `ITransactionalCommand`. Therefore an orchestrator that calls a provider must never implement that marker.

### 5.2 Public command catalog

Names are the implementation contract; combine request/handler/validator in the repository's established vertical-feature style.

| Capability | Public command | Transaction shape |
|---|---|---|
| Catalog | `UpsertProductMirror`, `CreatePrice`, `ArchivePrice` | local transaction |
| Settings | `UpdateAcademyPaymentSettings`, `ConfigureProviderAccount`, `ConfigureProviderCurrencyRoute` | local transaction |
| Checkout | `CreateCheckoutSession` | provider orchestrator |
| Webhook | `ReceiveProviderWebhook` | non-transactional verification entry; dispatches ingest phase |
| Webhook worker | `ProcessWebhookEvent` | orchestrates normalization/read then local domain phase |
| Session recovery | `RecoverStaleCheckoutSession` | provider orchestrator |
| Refund request | `RequestRefund`, `DecideRefundRequest` | local transaction |
| Provider refund | `IssueRefund` | provider orchestrator |
| Provider event | `ApplyNormalizedProviderEvent` | local transaction after normalization |
| Review | `OpenPaymentReview`, `DecidePaymentReview` | local transaction |
| Review resolution | `ApplyUnappliedFunds`, `ReturnUnappliedFunds`, `ReversePurchaseFromSupport` | local or provider orchestrator as movement requires |
| Manual correction | `RecordManualAdjustment` | local transaction resolving an approved review; requires `payments:AdjustLedger` |
| Settlement | `SettleAcademyPayable` | provider/manual-transfer orchestrator |
| Provider payout | `RecordProviderPayout` | local transaction after normalized movement |
| Reconciliation | `RunProviderReconciliation`, `RunInternalReconciliation` | orchestrator/read then local result phase |
| Archive | `ArchiveWebhookPayload`, `RedactWebhookPii` | object I/O around local transition phases |
| Operations | `RedriveWebhookEvent`, `RefreshProductPurchaseAnalytics` | local transaction |

The webhook endpoint passes provider name, headers, and exact raw bytes to `ReceiveProviderWebhook`. That command resolves the provider event-normalization capability, verifies the signature outside a transaction, and dispatches `IngestVerifiedProviderWebhook` only on success. Endpoint code performs request mapping only.

### 5.3 Internal phase catalog

Use phases only where an external side effect creates a real recovery boundary:

| Orchestrator | Before I/O phase | After I/O phase |
|---|---|---|
| `CreateCheckoutSession` | `EnsureCheckoutIntent` | `StampCheckoutResult` |
| `ReceiveProviderWebhook` | `IngestVerifiedProviderWebhook` | n/a; processing continues from durable outbox |
| `IssueRefund` | `EnsureRefundIntent` | `StampRefundCallResult` |
| `ReturnUnappliedFunds` | `EnsureUnappliedReturnIntent` | `ConfirmUnappliedReturn` |
| `SettleAcademyPayable` | `EnsureSettlementIntent` | `ConfirmSettlementTransfer` |
| `RunProviderReconciliation` | `StartReconciliationRun` | `ApplyReconciliationResults` |
| `ArchiveWebhookPayload` | `StartPayloadArchive` | `CompletePayloadArchive` |

Each phase is independently idempotent. “After” commands accept normalized results, not SDK response objects. Definitive rejection and ambiguous timeout are different results.

### 5.4 Public queries

- Available checkout methods for Product/Student.
- Student Purchase list/detail.
- Platform transaction list/detail.
- Academy transaction list/detail.
- Refund request/review queues.
- Payment Review Case detail.
- Provider-account/currency revenue and top Products.
- Settlement position by payable account.
- Reconciliation runs/findings and provider health.

Queries return DTOs, use no tracked Domain aggregates, and must name scope when reading unfiltered Provider/Ledger/Webhook/Audit tables. No query computes a cross-currency total.

### 5.5 Validation and authorization

FluentValidation covers shape and basic ranges. Handlers enforce trust-boundary facts under lock:

- tenant/user ownership;
- staff permission from [`docs/Permissions.md`](../../docs/Permissions.md);
- Provider Account/environment/currency/capability;
- current status and allowed transition;
- maker/checker separation;
- Remaining Refundable/Unapplied/Settleable cap;
- evidence and external movement identity.

For Q41, the handler locks Purchase, loads the Refund/Dispute policy snapshot, and asks `RefundabilityPolicy` for a reservation decision. It then mutates the separate RefundRequest and Refund roots in the same transaction. Refund confirmation uses the reservation and does not run the cap against itself. Do not perform a pre-check outside the transaction and assume it remains true.

Canonical staff authorization comes from [`docs/Permissions.md`](../../docs/Permissions.md): catalog commands use `payments:ManageCatalog`; Academy settings use `payments:ManageAcademySettings`; Provider Account/route commands use `payments:ManageProviders`; Refund decisions/issuance use `payments:IssueRefund`; opening and deciding reviews use `payments:OpenReview` / `payments:ApproveReview`; Settlement and ManualAdjustment resolution use `payments:RecordSettlement` / `payments:AdjustLedger`; operator re-drive/reconciliation uses `payments:RunReconciliation`; Academy/Platform reads use `payments:ViewAnalytics` / `payments:ViewPlatformAnalytics`. Archive, scheduled reconciliation, analytics refresh, and PII procedures run as authorized internal service operations, not under an invented staff permission. Student commands use ownership/tenant checks as defined in the permission reference.

---

## 6. Application ports

### Provider capabilities

```csharp
IProviderCapabilityRegistry
ICheckoutProvider
IRefundProvider
IProviderEventNormalizer
IProviderReconciler
```

The registry resolves by immutable Provider name and required capability. Missing capability fails before creating financial intent. ShamCash never implements refund/dispute operations. Event normalization emits provider-neutral facts; a normalized fact may be ignored only through an explicit typed outcome.

### Persistence and infrastructure seams

```csharp
ICheckoutStore
IWebhookStore
IMoneyMovementStore
IFinanceReviewStore
IPaymentsReadStore
ILedgerWriter
IWebhookPayloadArchive
ICourseAccessReader
IClock
```

- Stores align with deep workflow boundaries, not tables.
- `ILedgerWriter` executes the hardened PostgreSQL posting function with targeted SQL and returns the entry ID.
- `IWebhookPayloadArchive` uploads/downloads/verifies deterministic objects; it cannot mutate the DB manifest itself.
- `ICourseAccessReader` answers checkout eligibility only. Course access mutation happens asynchronously through Courses integration contracts.
- `IClock` is used for domain/application timestamps; DB `PostedAt` remains database clock.

Do not expose `DbSet`, `IQueryable`, EF entities, provider SDK clients, or generic repositories across these ports.

---

## 7. Infrastructure design

### 7.1 Persistence models

Infrastructure defines EF data models/configurations matching [`database.md`](database.md). Domain aggregates are mapped explicitly on load/save. Append-only records may be written directly from validated Domain/Application records without pretending they are mutable aggregates.

Persistence rules:

- explicit `Add` for new records;
- targeted `ExecuteUpdate` or SQL for tripwired status transitions;
- row-count mismatch is a concurrency/domain conflict, not success;
- no correctness based on “tracked object changed”;
- no lazy loading;
- no Payments entity implements `ISoftDeletable`/`ISoftDeleteItem`, and `SoftDeleteInterceptor` is not registered on `Payments.AppDbContext` — money rows are append-only and any DELETE must fail; the scaffold's interceptor registration is removed in Slice 1;
- the scaffold namespace/folder `Persistance` is corrected to `Persistence` in Slice 1 (folder, namespace and all `using`s);
- query DTOs use `AsNoTracking`/projection;
- Ledger writes only through `ILedgerWriter` and the database function.

### 7.2 Provider adapters

`Providers/Stripe` and `Providers/ShamCash` contain clients, wire DTOs, signature verifiers, normalizers, capability implementations, and fixture contract tests. Registration declares capabilities once. Startup fails when an enabled database provider lacks a matching code registration.

Secrets are resolved from KeyVault by stored secret names. Secret values never enter tables, logs, errors, fixtures, or audit payloads.

### 7.3 Webhook and outbox

The endpoint calls `ReceiveProviderWebhook` with immutable provider route data, headers, and raw bytes. Application selects the event-normalization capability and verifies the signature; the internal ingest phase writes WebhookEvent, WebhookEventPayload, and `ProcessWebhookEvent` through the existing Payments EF outbox in one transaction. Configure `QueryDelay ≤ 5 seconds`; the current scaffold's 5 minutes must change in the financial-kernel slice.

The consumer calls `ProcessWebhookEvent`. It never calls DbContext directly. Consumer redelivery, Provider Transaction dedupe, domain uniques, and state tripwires make processing idempotent. Sweeper re-enqueues stale work.

Application translates private Domain events into the public contracts in §8 only after the required Ledger audit reference is available. The translated event, Domain state, audit row, and Ledger posting commit in one transaction; Courses receives it after commit through the EF outbox. No handler invokes a Courses command directly. The scaffold's current `QueryDelay = 5 minutes` is corrected to ≤ 5 seconds in Slice 1.

### 7.4 Ledger writer

`ILedgerWriter` serializes a `LedgerPosting` to the versioned `post_ledger_entry(jsonb)` contract and executes it inside the ambient transaction. The migration:

- owns the function under the migration role;
- sets a fixed trusted search path and schema-qualifies objects;
- revokes PUBLIC execute;
- revokes runtime direct INSERT on entry/line tables;
- grants runtime EXECUTE only;
- retains deferred balance/provider/tenant/currency/minor-unit constraints and deny-mutation triggers.

No fallback direct-insert path exists.

### 7.5 Archive and workers

Archive worker uploads by deterministic EventId-derived key, verifies read-back SHA-256, then dispatches completion. The database clears hot JSON only during the verified lifecycle transition. Reader transparently resolves hot or verified cold content.

Workers for sweeper, reconciliation, analytics, and archive call public Application commands and persist runs/findings/watermarks. They do not embed financial policy.

---

## 8. Cross-module access design

Courses also owns the inbound catalog fact used by the Payments mirror:

```text
CoursePlanAvailabilityChangedIntegrationEvent(
  EventId, TenantId, CourseId, CoursePlanId, TargetResourceArn,
  Title, Availability, SourceVersion, OccurredAt)
```

`Availability` is `Available | Unavailable`; it is source lifecycle state, not a precomputed Payments Sellable flag. Courses emits a full snapshot on relevant publish/unpublish, Plan add/update/remove, and title changes. `SourceVersion` is monotonic per Course Plan. The Payments consumer maps it to `UpsertProductMirror`, applies only newer versions, and is repaired by initial backfill plus mirror reconciliation. Slice 1 owns this Courses producer/contract and Payments consumer so it does not depend on Slice 2. This inbound catalog contract is not a Courses access mutation; Payments still sends only the two access integration events below.

### Payments → Courses access

Courses owns one source-aware access capability:

```text
GrantCourseAccess(SourceType, SourceId, UserId, TenantId, TargetResourceArn, Metadata)
RevokeCourseAccess(SourceType, SourceId, UserId, TenantId, TargetResourceArn, Cause)
```

Payments never sends these commands directly. It publishes:

```text
PurchaseActivatedIntegrationEvent(..., AuditReference)
PurchaseAccessRevocationRequestedIntegrationEvent(..., CauseType, CauseId, AuditReference)
```

Courses consumers translate those Payments-owned facts into the Courses-owned commands above. Domain events remain private to Payments.

`SourceType` initially supports `AccessCode` and `Purchase`. The Courses change includes:

- replace or adapt `ProvisionAccessCodeCommand` behind the shared grant command;
- migrate `FailedProvisioningRecord.AccessCodeId` to `SourceType + SourceId`;
- migrate repository methods and unique index to `(TenantId, SourceType, SourceId)`;
- migrate revocation saga/state identity away from `AccessCodeId`;
- keep Library consumers as thin AccessCode mappings;
- add Payments consumers as thin Purchase mappings;
- preserve plan selection, Identity principal ensure, enrollment, failure parking, retry, and idempotent revoke behavior.

This is one Courses capability refactor, not a Payments copy. A migration/backfill marks existing rows `SourceType = AccessCode` and `SourceId = old AccessCodeId` before dropping/renaming the old field.

---

## 9. Failure modes

| Path | Failure | Handling | Observable outcome |
|---|---|---|---|
| Checkout create | provider accepted, app timed out | pending durable intent; retry/retrieve same key | no second charge/session |
| Webhook | process dies after ingest commit | EF outbox dispatch; sweeper repair | short delay, no lost paid access |
| Movement | two sources report same cash | Provider Transaction unique identity | loser is Duplicate; one posting |
| ShamCash | partial/late/extra/unmatched receipt | Unapplied Funds + review finding | no access; support-visible liability |
| Ledger | malformed or direct append | function/permissions/constraints reject transaction | no partial money state |
| Refund/dispute | concurrent reversal | Purchase lock + Remaining Refundable + uniques | one valid result; loser retries/conflicts |
| Review | maker is checker or evidence absent | authorization + DB CHECK | no money movement |
| Settlement | stale balance/reconciliation | exact payable lock and policy recompute | rejected before transfer; an already-confirmed transfer is booked once and creates a Critical stale-settlement finding |
| Access | Courses cannot resolve plan/principal | source-aware failed-provisioning park | money remains valid; ops can retry |
| Archive | upload/hash/DB transition failure | retain hot payload; deterministic retry | replay remains available |
| Provider health | one provider credential/route fails | degrade affected route only | other methods and history continue |
| Provider drift | fixture passes but live contract fails | sandbox gate reports BLOCKED | no release on inferred compatibility |

### 9.1 Critical scenario closure

| Scenario | Lock and Domain decision | DataModels and Ledger | Events / integration | Idempotency and retry outcome |
|---|---|---|---|---|
| Two simultaneous Refund approvals | lock RefundRequest then Purchase; `RefundabilityPolicy` admits one reservation and Request permits one terminal decision | winner updates `RefundRequests`, inserts Pending `Refund`, audit; no Ledger | request/refund Domain events; no integration event | request status + one-open/RefundId uniques; replay returns existing approval, competing stale decision conflicts and may reload |
| Confirm Pending Refund 60 while Q41 Remaining after its reservation is 40 | lock Refund then Purchase then payable; `Refund.Succeed` consumes its reservation and `Purchase.RecordConfirmedRefund` uses confirmed facts; Q41 is not rerun | ProviderTransaction, Refund Succeeded, Purchase status, `RefundReversal`, audit | Refund/Purchase Domain events; revocation only if full or flagged | movement, external-ref and RefundReversal uniques; duplicate confirmation is a no-op |
| Provider-originated Refund without local Request | lock/create Refund then Purchase then payable; provider movement truth is recorded even if it raced local capacity | ProviderTransaction, `Refund(Source=Provider)`, Purchase status, `RefundReversal`, audit; Critical finding if cap/payable becomes negative; no RefundRequest | Refund/Purchase Domain events; conditional revocation | provider movement/external refund/ledger uniques converge webhook and reconciliation; disagreement is Critical, retry reuses row |
| ShamCash provider-support reversal | approval locks ReviewCase then Purchase and creates Pending `Refund(Source=ProviderSupport)` from typed amount/revoke facts; confirmation locks case/refund/Purchase/payable and succeeds it | approval: case + Pending Refund + audit, no Ledger; confirmation: outgoing ProviderTransaction, Refund/Purchase transitions, `RefundReversal`, audit | review/refund/Purchase Domain events; conditional revocation | case/Refund/movement/Ledger uniques; ambiguous transfer remains approved + Pending and polls/retries with same intent |
| Dispute opened, no funds withdrawn | lock Dispute; `OpenFromProvider` records paperwork with zero exposure | Dispute Open + audit; no Ledger or Purchase status write | `DisputeOpenedDomainEvent`; no integration | provider dispute unique; replay is no-op; reconciliation waits for movement/close facts |
| Dispute funds withdrawn then reinstated | for each movement lock Dispute, Purchase, payable; record independent withdrawal/reinstatement facts; exposure appears then is removed | two ProviderTransactions, one `DisputeLoss`, one `DisputeWon`, audit; Dispute may remain Open | withdrawal/reinstatement Domain events; no Courses event | movement and `(DisputeId, Type)` uniques; either delivery order redrives until prerequisites exist |
| Lost Dispute after earlier partial Refund | lock Dispute then Purchase; Lost always requests revoke; `RecordLostDispute` advances Purchase only from confirmed withdrawal facts | Dispute Lost + audit; Purchase status advances when `DisputeLoss` exists; no Ledger at close | `DisputeResolvedDomainEvent(Lost)` + revocation using resolution audit ref | dispute transition/outbox identities; close-before-withdrawal revokes now and movement later redrives Purchase status only; Courses revoke is idempotent |
| Hosted checkout creation timeout | no lock spans I/O; intent phase commits Pending session, ambiguous result makes no transition | CheckoutSession remains Pending; no Purchase/Ledger | initial CheckoutSessionCreated only; no integration | CheckoutSessionId provider key + pending unique; retrieve/retry same key, never create a second attempt |
| Exact payment reported after CheckoutSession expiry | lock session then Purchase; `CompleteFromMovement` allows `Expired → Completed` when provider evidence proves a valid late observation (for ShamCash, occurrence was inside invoice window) | ProviderTransaction, Purchase, session completion retaining ExpiredAt, `Capture`, audit | session/Purchase Domain events + activation | session/movement/Purchase/Ledger uniques; callback/poll/reconciliation converge; money occurring after the valid window goes to Unapplied Funds instead |
| Duplicate webhook delivery references same movement | lock each WebhookEvent then target aggregate; first applies, later source observes existing movement/result | delivery rows/counts as applicable; one ProviderTransaction and one semantic Ledger entry; later outcome Duplicate | Domain/integration events only from winner | delivery unique plus movement/business-entry uniques; duplicate returns 2xx and retries are safe |
| Settlement approved, then payable changes before transfer confirmation | approval and preflight lock payable and run `SettlementPolicy`; confirmation locks payable again. No confirmed transfer: reject/re-approve. Confirmed transfer: record truth despite stale cap and flag it | typed ReviewCase intent; on confirmed transfer ProviderTransaction when applicable, Settlement, `Settlement` posting, audit, Critical finding; payable may become negative | review/Settlement Domain events; no integration | one in-flight case, PaymentReviewCaseId, external reference, movement and Ledger uniques; ambiguous transfer stays Approved and polls, confirmed replay is no-op |
| Purchase commits while Courses is unavailable | session lock; capture Domain decisions complete without calling Courses | ProviderTransaction, Purchase, session, `Capture`, audit and activation outbox commit atomically | private `PurchasePaidDomainEvent`; durable activation integration event | outbox retries; Courses inbox and `(TenantId, SourceType, SourceId)` make redelivery a no-op; access failure parks for retry without changing money |

---

## 10. Test architecture

Use xUnit, FluentAssertions, ASP.NET test host, and real PostgreSQL Testcontainers, matching repository conventions.

### Domain unit tests

- each aggregate's legal transitions, illegal-transition errors, expected previous status, and exact event;
- Q41 reservation at, above, and below Remaining Refundable, plus confirmation proving the Pending Refund is not counted twice;
- provider-originated and support Refund construction;
- CheckoutSession Pending/Expired → Completed late-money transitions;
- Dispute Opened vs FundsWithdrawn/FundsReinstated exposure;
- checkout eligibility matrix;
- Remaining Refundable and refund revocation decisions (Q41);
- Settleable Balance for Stripe and ShamCash policy (Q32/Q53);
- every Ledger posting shape, rounding and derived leg, plus `Money`/`LedgerPosting` invariant rejection;
- `Reconstitute` round-trips for every aggregate.

### Application tests

- orchestrator phase ordering, no transaction across provider I/O, timeout/retry/replay;
- capability absence before intent creation;
- authorization, tenant/provider/currency/environment isolation;
- maker/checker and evidence gates;
- normalized event dispatch and typed outcomes.

### PostgreSQL integration tests

- full migration up/down policy allowed by repo and schema creation;
- posting function, fixed search path, grants, PUBLIC/runtime denial, deferred balance checks, savepoints, rollback, replay, historical append denial;
- status triggers, partial uniques, minor units, typed reference sets;
- concurrent pending checkout, refund/dispute, unapplied review, and settlement races, including Open-without-withdrawal dispute exposure and Pending Refund reservation;
- archive lifecycle, hash/set-once fields, erasure procedure;
- EF outbox crash-after-ingest and duplicate processing.

### Cross-module integration tests

- Library AccessCode regression after source migration;
- Payments Purchase grant parity;
- identical UUID used once as AccessCode and once as Purchase does not collide;
- failure parking/retry by source identity;
- full/flagged refund and lost-dispute revoke exactly once;
- unflagged partial refund leaves access active.

### Provider contract tests

- deterministic signed Stripe and ShamCash fixtures run in PRs;
- fake capabilities drive all application replay/failure tests;
- secret-gated scheduled/on-demand sandbox smoke verifies live signatures, idempotency, normalization, and reconciliation;
- unavailable credentials produce a visible **BLOCKED** release gate.

### Performance and operations tests

- data-shaped 10× forecast benchmark against thresholds in `database.md` §16.6;
- archive upload/hash/restore/erasure;
- reconciliation overlap and movement disagreement;
- monthly PITR restore rehearsal with LedgerInternal reconciliation.

Architecture tests fail when Presentation/Consumers reference Infrastructure persistence/adapters, provider DTOs escape Infrastructure, an origin calls internal phases or Domain services directly, mapper/DataModel types leave Infrastructure, a Payments Domain type imports EF/MediatR/MassTransit/provider SDKs (the transitive `INotification` on Shared's `IDomainEvent` is tolerated), Domain invokes `ILedgerWriter`, a domain service performs I/O, a money DataModel implements `ISoftDeletable`, or a Ledger table gains a runtime direct-insert path. Integration tests prove `PurchasePaidDomainEvent` is translated to `PurchaseActivatedIntegrationEvent` with its Ledger audit reference and committed atomically; a separate Slice 2 test proves Courses consumes it idempotently.

---

## 11. Vertical slices and gates

### Slice 0 — Plan/design clearance

**Delivers:** synchronized requirements/database/shared/provider flows, permissions, glossary, this design.

**Gate:** met by the 2026-11-20 Slice 0 final review; no architecture or data-contract issue remains for Slice 1.

### Slice 1 — Financial kernel

**Delivers:** Academy/Provider configuration Domain Models; the Courses-owned `CoursePlanAvailabilityChangedIntegrationEvent` producer/contract and Payments Product-mirror consumer; immutable Price lifecycle; Checkout intent model; Provider Transactions; hardened Ledger posting; webhook ingest/outbox; a provider-neutral Purchase capture path driven by fake normalized movement; both Payments-owned access integration contracts; the activation producer only; core query seams, schema/roles/triggers, and Payments test projects. The scaffolded `IntegrationEvents` project drops its unnecessary Domain project reference so public contracts cannot depend on private Domain types.

**Gate:** only kernel Domain behavior is required: configuration, versioned catalog mirror (including initial backfill, out-of-order event, and reconciliation tests), Checkout eligibility/session lifecycle, Purchase capture, Provider Transaction identity, and Ledger posting. Migration/grant/function and transaction/outbox crash tests prove `PurchaseActivatedIntegrationEvent` is durable with Purchase + Ledger without requiring a Courses access consumer. Scaffold corrections are applied (`SoftDeleteInterceptor` removed, `QueryDelay ≤ 5s`, `Persistence` spelling, IntegrationEvents→Domain reference removed). Refundability waits for Slice 3 and SettlementPolicy waits for Slice 5; no provider SDK or Courses access consumer is needed for kernel acceptance.

### Slice 2 — Source-aware Courses access

May run in parallel with Slice 1 after Slice 0.

**Delivers:** shared Courses grant/revoke commands, source-aware failure/saga persistence migration, Courses consumers for the Slice 1 Payments integration contracts, and Library adapter/regressions.

**Gate:** Library regression, Purchase parity, cross-source collision, retry, and idempotent revoke tests.

Slices 1 and 2 merge before provider work.

### Slice 3 — ShamCash

**Delivers:** exact hosted invoice, callback normalization, polling/reconciliation, Unapplied Funds, review apply/return, `RefundabilityPolicy` as the first Refund-capable slice, support reversal and its revocation producer, and provider health. Settlement remains Slice 5.

**Gate:** full exact/non-exact fixture matrix, concurrency/review tests, data-shaped benchmark, secret-gated sandbox smoke. Missing sandbox access = BLOCKED.

### Slice 4 — Stripe

**Delivers:** Adaptive Pricing checkout/capture, refund requests/issuance, provider-originated refunds, disputes, payouts, reconciliation, risk holds, reuse of `RefundabilityPolicy`, and Stripe Refund/Dispute revocation producers.

**Gate:** presentment/settlement/fee truth, async refund, dispute timing, overlap/replay fixtures, benchmark, Stripe test-mode smoke. Missing account access = BLOCKED.

### Slice 5 — Operations and reporting

**Delivers:** Settlement policy and Wallet/manual-transfer settlement, settlement UI/API backing, five analytics, archive/redaction, reconciliation dashboards, monitoring/alerts, runbooks, and restore rehearsal.

**Gate:** maker/checker and stale-policy tests; archive restore/erasure; no mixed totals; benchmark thresholds; RPO/RTO rehearsal; legal/compliance release gates visible.

Each slice is independently deployable behind disabled provider routes. Schema evolution is additive; rollback disables new routes/consumers rather than deleting financial data.

### Dependency graph

```mermaid
flowchart LR
    S0[Slice 0: architecture] --> S1[Slice 1: financial kernel]
    S0 --> S2[Slice 2: Courses access]
    S1 --> S3[Slice 3: ShamCash]
    S2 --> S3
    S1 --> S4[Slice 4: Stripe]
    S2 --> S4
    S3 --> S5[Slice 5: operations]
    S4 --> S5
```

Slices 1 and 2 may run in parallel. Slices 3 and 4 may run in parallel after both 1 and 2 merge. Slice 5 follows both provider slices. Contracts flow forward only: Slice 1 owns the Courses catalog producer + Payments mirror consumer, defines activation/revocation contracts, and produces activation; Slice 2 consumes the access contracts; Slices 3/4 produce revocation. No provider SDK or Courses access consumer is a prerequisite of Slice 1.

---

## 12. Reuse and explicit non-goals

Reuse:

- existing Payments module projects, `TransactionalBehavior`, DbContext, and MassTransit EF outbox;
- tenant ambient context and module architecture conventions;
- Library immutable audit/analytics patterns where their invariants match;
- Courses plan selection, principal ensure, enrollment, failure parking, and revocation behavior;
- xUnit/FluentAssertions/Testcontainers stack;
- Shared `AggregateRoot`/`DomainEvent`/`IDomainEvent` and `IDataModelMapper`, plus Library's tripwire and `Reconstitute` patterns where they fit.

Do not add:

- one broad provider interface with unsupported operations;
- repository/service classes per table;
- generic “payments utilities” shared across modules;
- provider SDK types in Application/Domain;
- a second Payments-specific enrollment implementation;
- direct Ledger inserts or an EF fallback;
- process-local webhook handoff as the normal path;
- anemic aggregates — no Application handler may own a state transition, the refund/settlement cap, or a posting balance;
- soft-delete or DELETE semantics on money rows;
- subscriptions, discounts, tax, Connect, cross-currency totals, partitioning, or balance checkpoints without their separate approved trigger.

---

## 13. Review checklist

- [x] Every Payments plan header links this architecture and ownership is non-overlapping.
- [x] Public/internal command catalog matches `transaction_flows.md` and no provider call is transactional.
- [x] Q39–Q58 behavior matches `requirement.md` and `database.md`.
- [x] Posting-function grants/search path and archive lifecycle are migration-testable.
- [x] Courses source migration has backfill, uniqueness, Library regression, and Payments parity.
- [x] Provider capability matrix matches each provider document.
- [x] Slice gates include deterministic fixtures, secret-gated smoke, benchmark, and operations proof.
- [x] Domain Models and persistence DataModels remain separate; composite mappers are allowed and no table implies an aggregate boundary.
- [x] Q41 is evaluated once at Refund reservation from a snapshot loaded under the Purchase lock; confirmation cannot count its Pending Refund twice.
- [x] CheckoutSession supports valid late `Expired → Completed` movement and hosted-session creation never completes payment.
- [x] `PurchasePaidDomainEvent` remains private; Application emits `PurchaseActivatedIntegrationEvent` with its Ledger audit reference through the outbox.
- [x] Financial policies and `Money`/`LedgerPosting` live in Domain and are I/O-free; `ILedgerWriter` is called by Application, never Domain.
- [x] Dispute opening, funds withdrawal/reinstatement, and resolution are distinct facts.
- [x] No money entity is soft-deletable; no Payments Domain type imports MediatR/EF/MassTransit/provider SDKs.
- [x] Scaffold corrections (soft-delete interceptor, outbox `QueryDelay`, `Persistence` spelling, IntegrationEvents→Domain reference) are in Slice 1.
- [x] Root `DESIGN.md` remains untouched.
