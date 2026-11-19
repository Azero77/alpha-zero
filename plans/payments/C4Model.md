# Payments — C4 Model

**Derived from:** [`design.md`](design.md) §§2–8 (layers, ports, infrastructure, cross-module access) and [`requirement.md`](requirement.md) (actors and scope).
**Notation:** C4 levels expressed as Mermaid diagrams so they render in the repository. Colour key: blue = a Payments element, grey = external system, darker blue = person, light blue = component, cylinder-style label = data store. `design.md` remains the authority on implementation decisions.

---

## Level 1 — System context

### Purpose

Payments is the online economy: Students pay through enabled providers, receive course access through Courses, Academies accrue their share, and every movement is recorded in an append-only double-entry Ledger. It owns money truth; it does not own course access, identity, or provider-side money.

```mermaid
flowchart TB
  classDef person fill:#08427b,color:#ffffff,stroke:#053b6a;
  classDef system fill:#1168bd,color:#ffffff,stroke:#0b4884;
  classDef external fill:#999999,color:#ffffff,stroke:#6b6b6b;
  classDef datastore fill:#2b6ba3,color:#ffffff,stroke:#1d4d7a;

  student["Student<br/>checkout, purchases, refund asks"]:::person
  academy["Academy staff<br/>catalog, settings, analytics, reviews"]:::person
  platform["Platform staff<br/>refunds, reviews, settlement, operations"]:::person
  payments["Payments Module<br/>provider-neutral financial policy + Ledger"]:::system
  courses["Courses<br/>plans, enrollments, access grants/revocations"]:::external
  identity["Identity<br/>principals, tenant membership, permissions"]:::external
  library["Library<br/>Access Codes (offline economy)"]:::external
  stripe["Stripe<br/>hosted checkout, refunds, disputes, payouts"]:::external
  sham["ShamCash<br/>exact hosted invoices, callbacks, wallet"]:::external
  kv["KeyVault<br/>secret names resolved at runtime"]:::external
  storage["Object storage<br/>archived signed webhook payloads"]:::external
  monitor["Monitoring / sweeper<br/>health, redrive, stale work"]:::external

  student -->|"checkout / view purchases / request refund"| payments
  academy -->|"ManageCatalog, ManageAcademySettings,<br/>OpenReview, ViewAnalytics"| payments
  platform -->|"ManageProviders, IssueRefund, ApproveReview,<br/>RecordSettlement, RunReconciliation"| payments
  payments -->|"source-aware grant/revoke (Purchase)"| courses
  courses -->|"Product mirror events; eligibility fact"| payments
  payments -->|"permission + maker/checker checks"| identity
  library -->|"AccessCode parity after source migration"| courses
  payments -->|"checkout, refund, dispute, payout API"| stripe
  payments -->|"exact invoice, callback, wallet"| sham
  stripe -->|"signed webhooks"| payments
  sham -->|"signed callbacks"| payments
  payments -->|"secret names, never values"| kv
  payments -->|"archive / verify / restore / erase"| storage
  monitor -->|"redrive, re-enqueue stale work"| payments
```

### Key external relationships

| External | Direction | Contract |
|---|---|---|
| Courses | out | `GrantCourseAccess` / `RevokeCourseAccess`, `(SourceType=Purchase, SourceId)` via EF outbox |
| Courses | in | Product mirror events; `ICourseAccessReader` eligibility fact only — no direct access mutation |
| Stripe / ShamCash | both | capability-specific ports; signed events normalized to provider-neutral facts |
| Identity | in | permissions from `docs/Permissions.md`, tenant context, maker/checker identity |
| Library | indirect | same Courses capability with `SourceType=AccessCode`; regression parity, no Payments coupling |
| KeyVault / storage / monitoring | out | secret names, deterministic archive keys, redrive and health signals |

---

## Level 2 — Containers

```mermaid
flowchart TB
  classDef container fill:#438dd5,color:#ffffff,stroke:#2e6fa8;
  classDef datastore fill:#2b6ba3,color:#ffffff,stroke:#1d4d7a;
  classDef external fill:#999999,color:#ffffff,stroke:#6b6b6b;

  subgraph PM["Payments Module (deployable unit)"]
    pres["Presentation container<br/>FastEndpoints endpoints · webhook endpoints<br/>MassTransit consumers · workers"]:::container
    app["Application container<br/>public commands/queries · provider orchestrators<br/>internal transactional phases · ports"]:::container
    dom["Domain container<br/>aggregates + state · value objects<br/>pure policies · domain events"]:::container
    infra["Infrastructure container<br/>EF persistence + mappers/stores · hardened Ledger writer<br/>provider adapters · webhook/outbox · archive · behaviors"]:::container
    db[("Payments PostgreSQL<br/>schema, outbox/inbox,<br/>ledger function, roles, triggers")]:::datastore
  end

  origin["Origins: browser/API client, provider webhook,<br/>bus redelivery, timer worker"]:::external
  courses["Courses"]:::external
  identity["Identity"]:::external
  stripe["Stripe / ShamCash"]:::external
  kv["KeyVault / object storage"]:::external

  origin -->|"HTTP / webhook / message / timer"| pres
  pres -->|"public command or query only"| app
  app -->|"fetch facts, call one aggregate method"| dom
  app -->|"ports"| infra
  infra -->|"implements ports; maps to/from Domain"| dom
  infra -->|"EF Add/Update, ExecuteUpdate, SQL function,<br/>outbox rows"| db
  db -->|"QueryDelay <= 5s outbox delivery"| courses
  infra -->|"event handler writes grant/revoke outbox message"| courses
  app -->|"permission checks"| identity
  infra -->|"provider I/O, normalized facts back"| stripe
  infra -->|"secret names, archive objects"| kv
```

### Container responsibilities and hard rules

| Container | Responsibility | Rule |
|---|---|---|
| Presentation | input/output mapping only | no direct Domain service, port, DbContext or internal phase call |
| Application | use-case sequencing, authorization, locking, provider I/O boundaries | no financial rule re-implemented; orchestrators never carry `ITransactionalCommand` across provider I/O |
| Domain | aggregates, VOs, events, four policies, posting construction | no EF, MediatR, MassTransit, provider SDK, HTTP, object storage |
| Infrastructure | EF models/config/mappers/stores, Ledger function writer, adapters, outbox, archive, transaction behavior | no business decisions; row-count mismatch is a conflict, never success |
| Payments DB | append-only money rows, balance triggers, deny-mutation triggers, outbox/inbox, posting function | one runtime Ledger write path; no soft deletes, no direct inserts |

---

## Level 3 — Components

### 3a. Presentation

```mermaid
flowchart LR
  classDef component fill:#85bbf0,color:#0a3d62,stroke:#4a90c2;
  ep["Checkout / Purchase / Refund / Review / Settlement endpoints"]:::component
  wh["Signed webhook endpoints<br/>raw bytes + headers + provider name"]:::component
  con["MassTransit consumers<br/>ProcessWebhookEvent, Courses grant/revoke reactions"]:::component
  wrk["Workers<br/>sweeper, reconciliation, analytics, archive"]:::component
  bus[("EF outbox / inbox")]:::component
  ep --> bus
  wh --> bus
  con --> bus
  wrk --> bus
```

### 3b. Application

```mermaid
flowchart LR
  classDef component fill:#85bbf0,color:#0a3d62,stroke:#4a90c2;
  cmd["Public commands<br/>§5.2 catalog: catalog, checkout, webhook,<br/>aftercare, review, settlement, archive, ops"]:::component
  qry["Public queries / DTO projections<br/>§5.4 catalog"]:::component
  orch["Provider orchestrators<br/>create checkout, issue refund, settle,<br/>return funds, reconcile, archive"]:::component
  phase["Internal transactional phases<br/>Ensure… / Stamp… / Ingest… / Confirm…"]:::component
  cap["Capability ports<br/>IProviderCapabilityRegistry, ICheckoutProvider,<br/>IRefundProvider, IProviderEventNormalizer, IProviderReconciler"]:::component
  pers["Persistence / infra ports<br/>ICheckoutStore, IWebhookStore, IMoneyMovementStore,<br/>IFinanceReviewStore, IPaymentsReadStore, ILedgerWriter,<br/>IWebhookPayloadArchive, ICourseAccessReader, IClock"]:::component
  behavior["Behaviors<br/>Validation → TransactionalBehavior → UnitOfWork dispatch"]:::component
  behavior --> cmd
  cmd --> orch
  cmd --> phase
  cmd --> qry
  orch --> phase
  orch --> cap
  phase --> pers
  qry --> pers
```

### 3c. Domain

```mermaid
flowchart LR
  classDef component fill:#85bbf0,color:#0a3d62,stroke:#4a90c2;
  agg["Aggregates + private state<br/>Product, Price, CheckoutSession, Purchase,<br/>RefundRequest, Refund, Dispute, PaymentReviewCase"]:::component
  rec["Validated immutable records<br/>ProviderTransaction, Settlement, LedgerEntry/Lines,<br/>audit + reconciliation records"]:::component
  vo["Value objects<br/>Money, ProviderName, ProviderAccountRef, ProviderRoute,<br/>ExternalMovementIdentity, LedgerPosting, AccessSource"]:::component
  pol["Pure domain services<br/>CheckoutEligibilityPolicy, RefundabilityPolicy (Q41),<br/>SettlementPolicy (Q32/Q53), LedgerPostingFactory"]:::component
  evt["Domain events<br/>20 past-tense events raised inside methods;<br/>PurchasePaid is the access trigger"]:::component
  map["IDataModelMapper + Reconstitute<br/>explicit load/save mapping contract"]:::component
  agg --> evt
  rec --> evt
  pol --> vo
  agg --> pol
  map --> agg
```

### 3d. Infrastructure

```mermaid
flowchart LR
  classDef component fill:#85bbf0,color:#0a3d62,stroke:#4a90c2;
  ef["EF AppDbContext + Models/Configurations<br/>Mappers/Stores; explicit Add, targeted ExecuteUpdate;<br/>no tracker, no lazy loading"]:::component
  led["Hardened Ledger writer<br/>post_ledger_entry(jsonb) via ILedgerWriter;<br/>fixed search path, revoked direct INSERT"]:::component
  adapters["Provider adapters<br/>Providers/Stripe, Providers/ShamCash:<br/>clients, wire DTOs, signature verifiers, normalizers"]:::component
  webhook["Webhook ingest + EF outbox<br/>durable WebhookEvent/Payload,<br/>QueryDelay <= 5s, sweeper redrive"]:::component
  archive["Object archive<br/>deterministic key, read-back SHA-256,<br/>verified hot-to-cold lifecycle"]:::component
  tx["Behaviors / unit of work<br/>transaction open, event dispatch, SaveChanges, commit"]:::component
  ef --> led
  ef --> webhook
  adapters --> webhook
  webhook --> archive
  tx --> ef
```

---

## Level 4 — Dynamic views

### 4a. Checkout creation (provider orchestrator)

```mermaid
sequenceDiagram
  participant ST as Student
  participant API as Presentation
  participant APP as Application
  participant DOM as Domain
  participant DB as Payments DB
  participant PSP as Stripe / ShamCash
  ST->>API: CreateCheckoutSession
  API->>APP: public command (no transaction marker on orchestrator)
  APP->>DB: EnsureCheckoutIntent (transactional phase)
  DB->>DOM: CheckoutEligibilityPolicy + CheckoutSession.Create
  DOM-->>DB: CheckoutSessionCreated event
  DB-->>APP: committed intent + idempotency key
  APP->>PSP: create hosted session (outside transaction)
  PSP-->>APP: session result (definite or ambiguous timeout)
  APP->>DB: StampCheckoutResult (transactional phase)
  DOM->>DOM: Session.Complete / Expire / MarkAmbiguous
  DB-->>APP: committed state
  APP-->>API: session URL / status
  API-->>ST: redirect
```

### 4b. Capture webhook → Purchase → access grant

```mermaid
sequenceDiagram
  participant PSP as Stripe / ShamCash
  participant API as Webhook endpoint + consumer
  participant APP as Application
  participant DOM as Domain
  participant DB as Payments DB + outbox
  participant CRS as Courses
  PSP->>API: signed webhook (raw bytes + headers)
  API->>APP: ReceiveProviderWebhook
  APP->>APP: verify signature outside any transaction
  APP->>DB: IngestVerifiedProviderWebhook: event + payload + ProcessWebhookEvent outbox
  DB-->>APP: committed ingest
  Note over DB,CRS: outbox delivers within <= 5 seconds
  DB->>API: ProcessWebhookEvent consumer
  API->>APP: normalize to provider-neutral fact
  APP->>DB: ApplyNormalizedProviderEvent (transactional phase)
  DB->>DOM: ProviderTransaction.Record + Purchase.CreateFromCapture
  DOM->>DB: LedgerPosting via ILedgerWriter (only write path)
  DOM-->>DOM: PurchasePaid + LedgerEntryPosted raised
  DB->>DB: UnitOfWork dispatch → audit row + Courses grant outbox message
  DB-->>CRS: GrantCourseAccess(Purchase) — same transaction, idempotent
```

### 4c. Refund request → issuance → revocation

```mermaid
sequenceDiagram
  participant ST as Student
  participant PL as Platform refund staff
  participant API as Presentation
  participant APP as Application
  participant DOM as Domain
  participant DB as Payments DB + outbox
  participant PSP as Provider
  participant CRS as Courses
  ST->>API: RequestRefund
  API->>APP: local transactional command
  APP->>DOM: RefundRequest.Create (one open per Purchase)
  DB-->>APP: RefundRequested
  PL->>API: DecideRefundRequest (Approve + amount + RevokeAccess)
  APP->>DOM: RefundabilityPolicy (Q41) under Purchase lock
  DOM->>DOM: RefundRequest.Approve → RefundCreated
  APP->>PSP: IssueRefund (orchestrator, after intent phase)
  PSP-->>API: async result webhook
  APP->>DOM: Refund.Succeed → Purchase.ApplyRefund
  DOM-->>DOM: PartiallyRefunded or FullyRefunded
  DB->>DB: audit + revoke outbox message if RevokeAccess
  DB-->>CRS: RevokeCourseAccess(Purchase) — full or flagged partial only
```

### 4d. Settlement (maker/checker + risk policy)

```mermaid
sequenceDiagram
  participant PL as Platform finance staff
  participant CK as Different Platform checker
  participant PSP as Provider payout
  participant APP as Application
  participant DOM as Domain
  participant DB as Payments DB
  PL->>APP: SettleAcademyPayable → EnsureSettlementIntent
  APP->>DB: lock exact payable account FOR NO KEY UPDATE
  DB->>DOM: SettlementPolicy → Settleable Balance (Q32/Q53)
  DOM-->>APP: rejection reasons or approved amount
  APP->>CK: approval required (checker differs from maker)
  CK->>APP: DecidePaymentReview → Approved
  APP->>PSP: manual transfer evidence confirmed (outside transaction)
  APP->>DB: ConfirmSettlementTransfer
  DOM->>DB: Settlement.Record + balanced LedgerPosting
  DB-->>APP: SettlementRecorded + LedgerEntryPosted
```

---

## Constraints mapped to views

| Constraint | View / enforcement |
|---|---|
| Compile direction Presentation → Application → Domain; Infrastructure implements ports | L2, L3b–L3d; architecture tests |
| Transaction order: Validation → TransactionalBehavior (open) → UnitOfWork (dispatch + SaveChanges) → Commit | L3b, L4b; integration test |
| No provider I/O inside a transaction; orchestrators carry no `ITransactionalCommand` | L4a, L4c |
| Outbox `QueryDelay ≤ 5 seconds`; no external call in an event handler | L3d, L4b |
| Append-only money; no `ISoftDeletable`, no `SoftDeleteInterceptor`, direct Ledger INSERT revoked | L2 Payments DB, L3d |
| Tenant / provider / currency / environment / capability scope on every money read and command | L1 actors, L3b, §5.5 of `design.md` |
| Source-aware access is Courses-owned; identical UUIDs across AccessCode and Purchase must not collide | L1, L4b–L4c, [`ConceptualModel.md`](ConceptualModel.md) §6 |
| One reporting currency per provider account; no cross-currency totals | L1/L2 read models |

---

## Traceability to `design.md`

| C4 view | `design.md` source |
|---|---|
| L1 context | §1 goals 1–4, §8 cross-module access, `requirement.md` §1–3 |
| L2 containers | §2 dependency/runtime shape, §7 infrastructure design |
| L3a presentation | §5.2–§5.3 command/phase catalogs |
| L3b application | §5.1–§5.5, §6 ports |
| L3c domain | §4.1–§4.6 |
| L3d infrastructure | §7.1–§7.5 |
| L4 dynamics | §5.3 phase catalog, §7.3 outbox, §9 failure modes |
