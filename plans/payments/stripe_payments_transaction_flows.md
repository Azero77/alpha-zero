# Payments Module — Complete Transaction Flow Diagrams & Architectural Guide

This document provides end-to-end transaction flow diagrams, concurrency controls, double-entry ledger bookings, and idempotency guarantees for each transaction defined in §5 of [database.md](file:///home/azero/Desktop/AlphaZeroLearningAcademy/plans/payments/database.md).

---

## 🏛️ System Architecture & Global Concurrency Mandates

Before analyzing individual transactions, the system enforces three universal architectural patterns across all database operations:

### 1. Global Lock Order Hierarchy
To prevent deadlocks between capture webhooks, refunds, disputes, and settlements, all transactions acquire locks in strict descending hierarchy using `SELECT ... FOR NO KEY UPDATE`:

```mermaid
flowchart TD
    L1["1. WebhookEvents Row (FOR NO KEY UPDATE)"]
    L2["2. Primary Aggregate Row: CheckoutSessions / Refunds / Disputes"]
    L3["3. Purchases Row (FOR NO KEY UPDATE)"]
    L4["4. Academy Payable LedgerAccounts Row (FOR NO KEY UPDATE)"]
    L5["5. LedgerEntries & LedgerLines Inserts (Append-Only)"]

    L1 --> L2 --> L3 --> L4 --> L5
```

> [!IMPORTANT]
> `FOR NO KEY UPDATE` is required instead of `FOR UPDATE`. When `LedgerLines` are inserted with a foreign key referencing `LedgerAccounts(Id)`, PostgreSQL takes a `FOR KEY SHARE` lock on the account row. A plain `FOR UPDATE` lock conflicts with `FOR KEY SHARE`, causing concurrent capture insertions to deadlock or stall behind settlements. `FOR NO KEY UPDATE` serializes balance mutations while allowing concurrent capture FK checks to pass freely.

### 2. Two-Phase Webhook Ingestion
All webhook processing is decoupled into an ultra-fast HTTP ingestion phase and an asynchronous background processing phase:

```mermaid
sequenceDiagram
    autonumber
    participant PSP as Payment Service Provider (Stripe)
    participant API as Webhook Endpoint (HTTP Thread)
    participant DB as PostgreSQL Database
    participant Q as In-Memory Channel / Sweeper
    participant Worker as Background Processor

    PSP->>API: POST /api/payments/webhooks (Event Payload)
    Note over API: Signature verified BEFORE any DB write.<br/>Unsigned requests get 400 immediately.
    rect rgb(240, 248, 255)
        Note over API,DB: Phase 1: Autonomous Ingest Transaction
        API->>DB: INSERT INTO WebhookEvents + WebhookEventPayloads<br/>ON CONFLICT ("PspEventId") DO UPDATE<br/>SET DeliveryCount = DeliveryCount + 1
        DB-->>API: (Id, ProcessingOutcome)
    end
    alt Outcome already terminal (Processed / Duplicate / Skipped)
        API-->>PSP: 200 OK (Immediate exit)
    else First Delivery (Outcome IS NULL)
        API->>Q: Enqueue WebhookEvent.Id
        API-->>PSP: 200 OK (Sub-15ms response)
    end
    
    rect rgb(255, 250, 240)
        Note over Q,Worker: Phase 2: Asynchronous Domain Processing
        Q->>Worker: Dequeue WebhookEvent.Id
        Worker->>PSP: Read API calls outside DB tx (e.g. balance_transaction)
        Worker->>DB: Open TransactionalBehavior (Domain Tx)<br/>Lock WebhookEvents FOR NO KEY UPDATE<br/>Re-check ProcessingOutcome, mutate state, outbox, commit
    end
```

### 3. Chart of Accounts (Structural vs. Per-Academy)
- **`PSP Clearing`** (Asset): Cash collected and held by the PSP pending settlement.
- **`Platform Revenue`** (Income): AlphaZero platform commission (`CommissionShare`).
- **`PSP Fees`** (Expense): Direct processing and dispute fees deducted by the PSP.
- **`Bank Account`** (Asset): Platform physical bank account for payouts/settlements.
- **`PSP Suspense`** (Liability): Temporary holding account for unmatched captures.
- **`Academy Payable`** (Liability): One account per Academy (`TenantId`), tracking money owed to the Academy.

---

## Transaction 1: Student: Create Checkout Session

### Overview
- **Trigger:** Student initiates checkout (`POST /api/payments/checkout-sessions`).
- **One Transaction Contains:** Business eligibility check $\rightarrow$ `CheckoutSessions` insert (or reuse/refresh).
- **Idempotency & Concurrency:** Partial unique `(UserId, ProductId) WHERE Status = 'Pending'`. Deterministic `CheckoutSessionId` sent as PSP idempotency key.

### Flow Diagram

```mermaid
sequenceDiagram
    autonumber
    actor Student
    participant API as Payments API
    participant Enroll as Courses / Enrollment Service
    participant DB as PostgreSQL (Payments DbContext)
    participant PSP as Stripe / PSP Adapter

    Student->>API: POST /api/payments/checkout-sessions { ProductId }
    API->>Enroll: Check Active Enrollment (Eligibility Gate, Q33)
    alt Already Enrolled
        Enroll-->>API: Active Enrollment Found
        API-->>Student: 409 Conflict ("User already enrolled")
    else Eligible
        Enroll-->>API: Eligible
    end

    rect rgb(245, 245, 255)
        Note over API,DB: Pre-Transaction Validation
        API->>DB: Query Product (IsPurchasable == true)<br/>Query AcademyPaymentSettings (OnlineSellingEnabled == true)<br/>Query Active Price for Product & Currency
        DB-->>API: Product, Settings, PriceSnapshot
    end

    rect rgb(240, 255, 240)
        Note over API,DB: Session Lookup / Creation
        API->>DB: SELECT * FROM CheckoutSessions<br/>WHERE UserId = @Uid AND ProductId = @Pid AND Status = 'Pending'
        alt Existing Pending Session Found
            alt Locked Price Archived/Superseded
                API->>DB: UPDATE CheckoutSessions SET Status = 'Expired', ExpiredAt = now()<br/>WHERE Id = @ExistingSessionId
                Note over API: Fall through to create fresh session
            else Locked Price Still Active
                API->>PSP: Re-invoke PSP Create Session (Idempotency-Key: ExistingSessionId)
                PSP-->>API: Live Checkout Session URL (Replayed)
                API-->>Student: 200 OK { CheckoutUrl }
            end
        end
    end

    Note over API: Generate new CheckoutSessionId = Guid.NewGuid()
    rect rgb(255, 245, 238)
        Note over API,DB: DB Insert Transaction
        API->>DB: INSERT INTO CheckoutSessions (<br/>  Id = CheckoutSessionId, Status = 'Pending',<br/>  TenantId, UserId, ProductId, PriceId, CreatedAt = now()<br/>)
        Note over DB: Partial unique constraint (UserId, ProductId)<br/>WHERE Status = 'Pending' blocks racing double-clicks
        DB-->>API: Stored Durably
    end

    API->>PSP: POST /v1/checkout/sessions<br/>(Idempotency-Key: CheckoutSessionId)<br/>client_reference_id: CheckoutSessionId<br/>metadata: { TenantId, UserId, ProductId, PriceId, CheckoutSessionId }
    
    alt Definitive PSP Rejection (e.g. Card Brand Blocked)
        PSP-->>API: 4xx Terminal Rejection
        API->>DB: UPDATE CheckoutSessions SET Status = 'Expired', ExpiredAt = now()
        API-->>Student: 400 Bad Request (PSP Error)
    else Timeout / Network Glitch (5xx)
        Note over API: Stays Pending! (Q28: Ambiguity is money-safe)<br/>Student may still pay on PSP side.
        API-->>Student: 504 Gateway Timeout (Retryable)
    else PSP Success
        PSP-->>API: 200 OK { id: "cs_123...", url: "https://checkout.stripe.com/..." }
        API->>DB: UPDATE CheckoutSessions SET PspCheckoutSessionId = 'cs_123...'
        API-->>Student: 201 Created { CheckoutUrl }
    end
```

---

## Transaction 2: Webhook: `checkout.session.completed`

### Overview
- **Trigger:** PSP webhook notifying successful student payment.
- **One Transaction Contains:** Lock session `FOR NO KEY UPDATE` $\rightarrow$ Metadata-first resolution $\rightarrow$ Missing session $\Rightarrow$ `UnmatchedCapture` to `PSP Suspense` $\rightarrow$ `PspCustomers` ensure $\rightarrow$ `Purchases` insert (`Paid`) $\rightarrow$ Double-entry `Capture` booking $\rightarrow$ Outbox `PurchaseActivatedIntegrationEvent` $\rightarrow$ Session `Completed` $\rightarrow$ Audit row.
- **Accounting Split ($G$ = Gross, $c$ = Commission share, $F$ = Fee):**
  - $\text{PlatformRevenue} = \text{ROUND}(G \times c, \text{minor-unit})$
  - $\text{AcademyPayable} = G - \text{PlatformRevenue}$ (derived leg)
  - **Dr** `PSP Clearing`: $G$
  - **Cr** `Academy Payable` (Tenant): $\text{AcademyPayable}$
  - **Cr** `Platform Revenue`: $\text{PlatformRevenue}$
  - *(When fee $F > 0$)*: **Dr** `PSP Fees`: $F$ / **Cr** `PSP Clearing`: $F$

### Flow Diagram

```mermaid
sequenceDiagram
    autonumber
    participant PSP as Stripe
    participant Worker as Background Processor
    participant DB as PostgreSQL Database
    participant Outbox as MassTransit Outbox Table
    participant Courses as Courses Module

    Note over Worker: Phase 2 Begins: Read expanded PSP session outside DB Tx
    Worker->>PSP: GET /v1/checkout/sessions/cs_123?expand[]=payment_intent.balance_transaction
    PSP-->>Worker: Session Details { Gross G, Fee F, Net, Currency, Metadata }

    rect rgb(240, 248, 255)
        Note over Worker,DB: Domain DB Transaction (TransactionalBehavior)
        Worker->>DB: SELECT * FROM WebhookEvents WHERE Id = @EventId FOR NO KEY UPDATE
        Note over Worker: Check ProcessingOutcome. If not null, exit (duplicate).

        Worker->>DB: SELECT * FROM CheckoutSessions<br/>WHERE Id = metadata.CheckoutSessionId (Fallback: PspCheckoutSessionId)<br/>FOR NO KEY UPDATE
        
        alt Session Missing in Database
            Note over Worker,DB: Unmatched Capture Handling (Q28)
            Worker->>DB: INSERT INTO LedgerEntries (Type = 'UnmatchedCapture', PspObjectId = 'pi_...', Description = 'Orphan capture')
            Worker->>DB: INSERT INTO LedgerLines: Dr PSP Clearing G / Cr PSP Suspense G
            Worker->>DB: UPDATE WebhookEvents SET ProcessingOutcome = 'Processed'
            Worker->>DB: INSERT INTO ReconciliationFindings (Severity = 'Critical', Detail = 'Unmatched capture')
            Note over Worker: Commit & Alert Ops. Never mark Skipped!
        else Session Found in State 'Completed'
            Worker->>DB: UPDATE WebhookEvents SET ProcessingOutcome = 'Duplicate'
            Note over Worker: Commit & Return (Idempotent 200)
        else Session State 'Pending' OR 'Expired' (Late Payment, Q28)
            Worker->>DB: INSERT INTO PspCustomers (UserId, PspCustomerId)<br/>ON CONFLICT (UserId) DO NOTHING
            
            Note over Worker,DB: Create Purchase (The Money Row)
            Worker->>DB: INSERT INTO Purchases (<br/>  Id = Guid.NewGuid(), Status = 'Paid', PaidAt = now(),<br/>  PriceId = lockedPrice.Id, SettlementAmount = G, PspFee = F,<br/>  CommissionShare = c, PspPaymentIntentId = 'pi_...', PspChargeId = 'ch_...'<br/>)

            Note over Worker,DB: Double-Entry Ledger Booking (Capture)
            Worker->>DB: INSERT INTO LedgerEntries (Type = 'Capture', PurchaseId = Purchase.Id, TenantId = Tenant.Id)
            Worker->>DB: INSERT INTO LedgerLines:<br/>  Dr PSP Clearing: G<br/>  Cr Academy Payable: (G - ROUND(G * c))<br/>  Cr Platform Revenue: ROUND(G * c)
            opt If PSP Fee F > 0
                Worker->>DB: INSERT INTO LedgerLines:<br/>  Dr PSP Fees: F<br/>  Cr PSP Clearing: F
            end

            Worker->>Outbox: INSERT INTO OutboxMessage (<br/>  Type = PurchaseActivatedIntegrationEvent,<br/>  Payload = { PurchaseId, UserId, TargetResourceArn, AuditRef = LedgerEntry.Id }<br/>)

            Worker->>DB: UPDATE CheckoutSessions SET Status = 'Completed', CompletedAt = now()
            Worker->>DB: UPDATE WebhookEvents SET ProcessingOutcome = 'Processed'
            Worker->>DB: INSERT INTO PaymentsAuditLogs (Action = 'Purchase.Paid')
        end
    end

    Note over DB: Commit Transaction (Triggers verify ΣDr = ΣCr and same-tx lines)
    Outbox->>Courses: Dispatch PurchaseActivatedIntegrationEvent (Grants Enrollment)
```

---

## Transaction 3: Webhook: `checkout.session.expired`

### Overview
- **Trigger:** PSP sends `checkout.session.expired` after checkout window closes (e.g. 24 hours).
- **One Transaction Contains:** Ingest row $\rightarrow$ Mark session `Expired` $\rightarrow$ Audit row.
- **Guarantees:** Late payment safety (Q28): an expired session can legally transition to `Completed` if a racing capture occurs.

### Flow Diagram

```mermaid
flowchart TD
    Start(["Webhook checkout.session.expired"]) --> P1["Phase 1: Ingest into WebhookEvents"]
    P1 --> P2["Phase 2: Worker opens DB Transaction"]
    P2 --> Lock["Lock WebhookEvents row FOR NO KEY UPDATE"]
    Lock --> CheckOutcome{"ProcessingOutcome<br/>already set?"}
    CheckOutcome -- Yes --> ExitDup["Mark Duplicate & Commit"]
    CheckOutcome -- No --> Resolve["Resolve CheckoutSessions via metadata or PspCheckoutSessionId"]
    Resolve --> CheckStatus{"Session Status?"}
    CheckStatus -- "Completed" --> Ignore["Leave as Completed (Late capture won race)"]
    CheckStatus -- "Expired" --> NoOp["Already Expired (No-op)"]
    CheckStatus -- "Pending" --> Expire["UPDATE CheckoutSessions<br/>SET Status = 'Expired', ExpiredAt = now()"]
    Expire --> MarkProcessed["UPDATE WebhookEvents SET ProcessingOutcome = 'Processed'"]
    Ignore --> MarkProcessed
    NoOp --> MarkProcessed
    MarkProcessed --> Audit["INSERT INTO PaymentsAuditLogs (Action = 'Session.Expired')"]
    Audit --> Commit(["Commit Transaction"])
```

---

## Transaction 4: Platform Admin: Issue Refund

### Overview
- **Trigger:** Platform admin initiates a partial or full refund (`POST /api/payments/refunds`).
- **One Transaction Contains:** Lock `Purchases` row `FOR NO KEY UPDATE` $\rightarrow$ Open-dispute guard $\rightarrow$ Remaining Refundable calculation $\rightarrow$ Insert `Refunds` (`Pending`) $\rightarrow$ Audit row $\rightarrow$ Outbound PSP call outside transaction.
- **Remaining Refundable Formula (Q41):**
  $$\text{Remaining} = \text{Purchase.SettlementAmount} - \sum(\text{Pending} + \text{Succeeded Refunds}) - \sum(\text{Disputes Withdrawn [Open] or Lost})$$

### Flow Diagram

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Platform Admin
    participant API as Payments API
    participant DB as PostgreSQL Database
    participant PSP as Stripe API

    Admin->>API: POST /api/payments/refunds { PurchaseId, Amount, ReasonCode, RevokeAccess }
    
    rect rgb(240, 248, 255)
        Note over API,DB: Phase A: Local DB Transaction (Durable Intent)
        API->>DB: BEGIN TRANSACTION
        API->>DB: SELECT * FROM Purchases WHERE Id = @PurchaseId FOR NO KEY UPDATE
        
        API->>DB: SELECT COUNT(*) FROM Disputes WHERE PurchaseId = @PurchaseId AND Status = 'Open'
        alt Open Dispute Exists
            Note over API: Invariant 31: Blocked while disputed
            API-->>Admin: 409 Conflict ("Cannot refund a purchase with an active dispute")
        end

        API->>DB: Calculate Remaining Refundable under lock (Q41)
        alt Amount > Remaining Refundable
            API-->>Admin: 422 Unprocessable Entity ("Amount exceeds remaining refundable")
        end

        Note over API: Generate RefundId = Guid.NewGuid()
        API->>DB: INSERT INTO Refunds (<br/>  Id = RefundId, PurchaseId = @PurchaseId, Amount = @Amount,<br/>  Source = 'Platform', Status = 'Pending', ReasonCode = @ReasonCode,<br/>  RevokeAccess = @RevokeAccess, RequestedByUserId = @AdminId, CreatedAt = now()<br/>)
        API->>DB: INSERT INTO PaymentsAuditLogs (Action = 'Refund.Issued')
        API->>DB: COMMIT TRANSACTION
    end

    Note over API: RefundId is durable BEFORE network I/O
    rect rgb(255, 250, 240)
        Note over API,PSP: Phase B: Outbound PSP API Call
        API->>PSP: POST /v1/refunds<br/>(Idempotency-Key: RefundId)<br/>payment_intent: Purchase.PspPaymentIntentId, amount: Amount<br/>metadata: { RefundId: RefundId, PurchaseId: PurchaseId }
        alt Definitive Rejection (e.g. charge already refunded)
            PSP-->>API: 4xx Error
            API->>DB: UPDATE Refunds SET Status = 'Failed', CompletedAt = now() WHERE Id = RefundId
            API-->>Admin: 400 Bad Request (PSP Error)
        else Ambiguous Timeout / 5xx
            Note over API: Stays Pending! Handled by webhook or sweeper.
            API-->>Admin: 202 Accepted ("Refund pending confirmation")
        else PSP Success
            PSP-->>API: 200 OK { id: "re_123...", status: "succeeded" }
            Note over API: Webhook (refund.created / updated) will book the ledger!
            API-->>Admin: 200 OK { RefundId, Status: "Pending" }
        end
    end
```

---

## Transaction 5: Webhook: Refund Family (`refund.created` / `updated` / `failed`)

### Overview
- **Trigger:** Webhook from PSP regarding refund lifecycle.
- **One Transaction Contains:** Ingest $\rightarrow$ Resolve Purchase $\rightarrow$ Unknown refund $\Rightarrow$ Insert `Refunds` (`Source = 'Psp'`) $\rightarrow$ Transition `Pending → Succeeded` $\rightarrow$ Recompute Purchase status ladder $\rightarrow$ Pro-rata `RefundReversal` ledger booking $\rightarrow$ Outbox revocation (if full refund or `RevokeAccess = true`) $\rightarrow$ Audit row.
- **Reversal Ledger Entries:**
  - $\text{CommissionLeg} = \text{ROUND}(\text{refund.Amount} \times \text{Purchase.CommissionShare}, \text{minor-unit})$
  - $\text{AcademyLeg} = \text{refund.Amount} - \text{CommissionLeg}$
  - **Dr** `Academy Payable` (Tenant): $\text{AcademyLeg}$
  - **Dr** `Platform Revenue`: $\text{CommissionLeg}$
  - **Cr** `PSP Clearing`: $\text{refund.Amount} - \text{PspFeeRefunded}$
  - *(When fee refunded $> 0$)*: **Cr** `PSP Fees`: $\text{PspFeeRefunded}$

### Flow Diagram

```mermaid
sequenceDiagram
    autonumber
    participant PSP as Stripe
    participant Worker as Background Processor
    participant DB as PostgreSQL Database
    participant Outbox as MassTransit Outbox Table
    participant Courses as Courses Module

    Worker->>DB: BEGIN TRANSACTION (Domain Tx)
    Worker->>DB: SELECT * FROM WebhookEvents WHERE Id = @EventId FOR NO KEY UPDATE
    Worker->>DB: Resolve Purchase via metadata or PspRefundId

    alt Unknown Refund (PSP Dashboard Initiated, Q31)
        Worker->>DB: INSERT INTO Refunds (<br/>  Id = Guid.NewGuid(), PurchaseId = Purchase.Id, Amount = payload.Amount,<br/>  Source = 'Psp', Status = 'Pending', RequestedByUserId = NULL,<br/>  PspRefundId = payload.Id, CreatedAt = now()<br/>)
    end

    alt Webhook is refund.failed
        Worker->>DB: UPDATE Refunds SET Status = 'Failed', CompletedAt = now()
        Worker->>DB: UPDATE WebhookEvents SET ProcessingOutcome = 'Processed'
        Worker->>DB: COMMIT & Alert Ops (No ledger writes)
    else Webhook is refund.created / refund.updated (status = succeeded)
        Worker->>DB: UPDATE Refunds SET Status = 'Succeeded', CompletedAt = now()<br/>WHERE Id = @RefundId AND Status = 'Pending'
        Note over Worker,DB: Guard wins: exactly once execution

        Worker->>DB: SELECT * FROM Purchases WHERE Id = @PurchaseId FOR NO KEY UPDATE
        Worker->>DB: Calculate Total Succeeded Reversals (Σ Refunds + Σ Lost Disputes)
        alt Total Reversals >= Purchase.SettlementAmount
            Worker->>DB: UPDATE Purchases SET Status = 'Refunded', RefundedAt = now()
        else Total Reversals > 0
            Worker->>DB: UPDATE Purchases SET Status = 'PartiallyRefunded'
        end

        Worker->>DB: SELECT * FROM LedgerAccounts<br/>WHERE TenantId = @TenantId AND Name = 'Academy Payable'<br/>FOR NO KEY UPDATE
        Note over Worker,DB: Balance is never checked (can go negative / clawback)

        Worker->>DB: INSERT INTO LedgerEntries (Type = 'RefundReversal', RefundId = @RefundId, PurchaseId = @PurchaseId)
        Worker->>DB: INSERT INTO LedgerLines:<br/>  Dr Academy Payable: AcademyLeg<br/>  Dr Platform Revenue: CommissionLeg<br/>  Cr PSP Clearing: (Amount - PspFeeRefunded)<br/>  [Cr PSP Fees: PspFeeRefunded (if > 0)]

        opt Revocation Condition Met: (Purchase == 'Refunded' OR Refund.RevokeAccess == TRUE)
            Worker->>Outbox: INSERT INTO OutboxMessage (<br/>  Type = PurchaseRefundedIntegrationEvent,<br/>  Payload = { PurchaseId, UserId, RefundId = @RefundId, DisputeId = null, AuditRef = LedgerEntry.Id }<br/>)
        end

        Worker->>DB: UPDATE WebhookEvents SET ProcessingOutcome = 'Processed'
        Worker->>DB: INSERT INTO PaymentsAuditLogs (Action = 'Purchase.Refunded')
        Worker->>DB: COMMIT TRANSACTION
    end

    opt If Outbox Message Dispatched
        Outbox->>Courses: Revoke Enrollment Access
    end
```

---

## Transaction 6: Webhook: `charge.dispute.created`

### Overview
- **Trigger:** Webhook from PSP indicating a dispute has been opened by the cardholder.
- **One Transaction Contains:** Ingest row $\rightarrow$ Resolve Purchase via charge/PaymentIntent $\rightarrow$ Insert `Disputes` row (`Status = 'Open'`) $\rightarrow$ Audit row.
- **Critical Policy (Q36):** **NO LEDGER WRITE**. Funds are not moved at creation; physical withdrawal is reported via `funds_withdrawn`.

### Flow Diagram

```mermaid
flowchart TD
    Start(["Webhook charge.dispute.created"]) --> Ingest["Phase 1: Ingest & Enqueue"]
    Ingest --> Tx["Phase 2: Open DB Transaction"]
    Tx --> LockHook["Lock WebhookEvents FOR NO KEY UPDATE"]
    LockHook --> Resolve["Resolve Purchase via PspChargeId / PspPaymentIntentId"]
    Resolve --> InsertDispute["INSERT INTO Disputes (<br/>  Id = Guid.NewGuid(), PurchaseId = Purchase.Id, TenantId = Purchase.TenantId,<br/>  PspDisputeId = payload.Id, Status = 'Open', Amount = payload.Amount,<br/>  PspDisputeFee = payload.Fee, Reason = payload.Reason, OpenedAt = now()<br/>)"]
    InsertDispute --> NoLedger["NOTE: Zero ledger entries written (Q36)"]
    NoLedger --> Audit["INSERT INTO PaymentsAuditLogs (Action = 'Dispute.Created')"]
    Audit --> DoneHook["UPDATE WebhookEvents SET ProcessingOutcome = 'Processed'"]
    DoneHook --> Commit(["Commit Transaction"])
```

---

## Transaction 7: Webhook: `charge.dispute.funds_withdrawn`

### Overview
- **Trigger:** PSP withdraws the disputed amount plus chargeback fee from platform PSP balance.
- **One Transaction Contains:** Ingest row $\rightarrow$ `DisputeLoss` entry booked **immediately and unconditionally** $\rightarrow$ Payable locked $\rightarrow$ Audit row.
- **Clawback Rule:** Books even if the Purchase was already `Refunded` (Academy Payable goes negative, documented clawback).
- **Ledger Entries:**
  - $\text{PlatformLeg} = \text{ROUND}(\text{dispute.Amount} \times \text{CommissionShare}, \text{minor-unit})$
  - $\text{AcademyLeg} = \text{dispute.Amount} - \text{PlatformLeg}$
  - **Dr** `Academy Payable` (Tenant): $\text{AcademyLeg}$
  - **Dr** `Platform Revenue`: $\text{PlatformLeg}$
  - **Dr** `PSP Fees`: $\text{PspDisputeFee}$
  - **Cr** `PSP Clearing`: $\text{dispute.Amount} + \text{PspDisputeFee}$

### Flow Diagram

```mermaid
flowchart TD
    Start(["Webhook charge.dispute.funds_withdrawn"]) --> Tx["Begin DB Transaction"]
    Tx --> LockHook["Lock WebhookEvents FOR NO KEY UPDATE"]
    LockHook --> Resolve["Resolve Disputes row via PspDisputeId"]
    Resolve --> LockAccount["Lock Academy Payable LedgerAccounts FOR NO KEY UPDATE"]
    LockAccount --> CheckDedupe["Check partial unique (DisputeId, Type = 'DisputeLoss')"]
    CheckDedupe --> BookLoss["INSERT INTO LedgerEntries (<br/>  Type = 'DisputeLoss', DisputeId = Dispute.Id,<br/>  PurchaseId = Dispute.PurchaseId, TenantId = Dispute.TenantId<br/>)"]
    BookLoss --> BookLines["INSERT INTO LedgerLines:<br/>  Dr Academy Payable: (DisputeAmount - CommissionLeg)<br/>  Dr Platform Revenue: CommissionLeg<br/>  Dr PSP Fees: PspDisputeFee<br/>  Cr PSP Clearing: (DisputeAmount + PspDisputeFee)"]
    BookLines --> ClawbackNotice["Note: If account balance < 0, legitimate negative balance (Clawback)"]
    ClawbackNotice --> Audit["INSERT INTO PaymentsAuditLogs (Action = 'Dispute.FundsWithdrawn')"]
    Audit --> MarkDone["UPDATE WebhookEvents SET ProcessingOutcome = 'Processed'"]
    MarkDone --> Commit(["Commit Transaction"])
```

---

## Transaction 8: Webhook: `charge.dispute.funds_reinstated` (Won)

### Overview
- **Trigger:** Bank rules in platform's favor; funds returned to PSP balance.
- **One Transaction Contains:** Ingest $\rightarrow$ `DisputeWon` entry $\rightarrow$ Restores pro-rata allocations $\rightarrow$ Audit row.
- **Fee Rule:** The dispute fee is retained by the PSP and stays expensed.
- **Ledger Entries:**
  - **Dr** `PSP Clearing`: $\text{dispute.Amount}$
  - **Cr** `Academy Payable` (Tenant): $\text{AcademyLeg}$
  - **Cr** `Platform Revenue`: $\text{PlatformLeg}$

### Flow Diagram

```mermaid
flowchart TD
    Start(["Webhook charge.dispute.funds_reinstated"]) --> Tx["Begin DB Transaction"]
    Tx --> LockHook["Lock WebhookEvents FOR NO KEY UPDATE"]
    LockHook --> Resolve["Resolve Disputes row via PspDisputeId"]
    Resolve --> LockAccount["Lock Academy Payable LedgerAccounts FOR NO KEY UPDATE"]
    LockAccount --> BookWon["INSERT INTO LedgerEntries (<br/>  Type = 'DisputeWon', DisputeId = Dispute.Id, TenantId = Dispute.TenantId<br/>)"]
    BookWon --> BookLines["INSERT INTO LedgerLines:<br/>  Dr PSP Clearing: DisputeAmount<br/>  Cr Academy Payable: AcademyLeg<br/>  Cr Platform Revenue: PlatformLeg<br/>  (PSP Dispute Fee stays expensed)"]
    BookLines --> Audit["INSERT INTO PaymentsAuditLogs (Action = 'Dispute.FundsReinstated')"]
    Audit --> MarkDone["UPDATE WebhookEvents SET ProcessingOutcome = 'Processed'"]
    MarkDone --> Commit(["Commit Transaction"])
```

---

## Transaction 9: Webhook: `charge.dispute.closed`

### Overview
- **Trigger:** PSP notifies dispute case is officially closed (`Won` or `Lost`).
- **One Transaction Contains:** Ingest row $\rightarrow$ `Disputes` tripwire update (`Won`/`Lost`) $\rightarrow$ If `Lost`: recompute Purchase status ladder $\rightarrow$ Outbox `PurchaseRefundedIntegrationEvent` $\rightarrow$ Audit row.
- **Critical Policy:** **NO MONEY MOVEMENT AT CLOSE** (cash was moved at `funds_withdrawn` / `funds_reinstated`). **ANY** lost dispute revokes course enrollment access immediately.

### Flow Diagram

```mermaid
sequenceDiagram
    autonumber
    participant PSP as Stripe
    participant Worker as Background Processor
    participant DB as PostgreSQL Database
    participant Outbox as MassTransit Outbox Table
    participant Courses as Courses Module

    Worker->>DB: BEGIN TRANSACTION
    Worker->>DB: SELECT * FROM WebhookEvents WHERE Id = @EventId FOR NO KEY UPDATE
    Worker->>DB: SELECT * FROM Disputes WHERE PspDisputeId = @PspDisputeId FOR NO KEY UPDATE

    Worker->>DB: UPDATE Disputes SET Status = @WonOrLost, ResolvedAt = now(), OutcomeNotes = @Notes<br/>WHERE Id = @DisputeId AND Status = 'Open'
    
    alt Dispute Outcome == 'Lost'
        Worker->>DB: SELECT * FROM Purchases WHERE Id = Dispute.PurchaseId FOR NO KEY UPDATE
        Worker->>DB: Recompute Σ reversals (Refunds + Lost Disputes)
        opt If Σ reversals >= Purchase.SettlementAmount
            Worker->>DB: UPDATE Purchases SET Status = 'Refunded', RefundedAt = now()
        end
        Note over Worker,DB: Revocation rule: ANY lost dispute revokes access (Bank ruling)
        Worker->>Outbox: INSERT INTO OutboxMessage (<br/>  Type = PurchaseRefundedIntegrationEvent,<br/>  Payload = { PurchaseId, UserId, DisputeId = Dispute.Id, RefundId = null, AuditRef = DisputeLossEntry.Id }<br/>)
    else Dispute Outcome == 'Won'
        Note over Worker: Sale stands. Access remains granted. Zero outbox messages.
    end

    Note over Worker: Zero money movement here! (Q36)
    Worker->>DB: UPDATE WebhookEvents SET ProcessingOutcome = 'Processed'
    Worker->>DB: INSERT INTO PaymentsAuditLogs (Action = 'Dispute.Closed')
    Worker->>DB: COMMIT TRANSACTION

    opt If Lost
        Outbox->>Courses: Revoke Enrollment Access
    end
```

---

## Transaction 10: Platform Admin: Record Settlement

### Overview
- **Trigger:** Platform admin executes payout to an Academy (`POST /api/payments/settlements`).
- **One Transaction Contains:** Lock Academy Payable row `FOR NO KEY UPDATE` $\rightarrow$ Compute Settleable Balance $\rightarrow$ Amount guard $\rightarrow$ `Settlements` insert $\rightarrow$ `Settlement` ledger entry + 2 lines $\rightarrow$ Audit row.
- **Two-Tier Settleable Balance Formula (Q32, Q41):**
  $$\begin{aligned}
  \text{Settleable Balance} &= \text{Ledger Balance } (\Sigma\text{Cr} - \Sigma\text{Dr}) \\
  &- \Sigma\text{ Live Refunds (Pending)} \\
  &- 100\% \text{ of Academy legs for captures inside Short Hold window } (\le \text{ShortHoldDays, def 14d}) \\
  &- \text{ReserveRate (def 10\%)} \times \text{Academy legs in rolling dispute window } (14\text{d} \dots 120\text{d}) \\
  &- \Sigma\text{ remaining Academy legs for Purchases with open RefundRequests}
  \end{aligned}$$
- **Ledger Entries:**
  - **Dr** `Academy Payable` (Tenant): $\text{Amount}$
  - **Cr** `Bank Account` (Platform Asset): $\text{Amount}$

### Flow Diagram

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Platform Admin
    participant API as Payments API
    participant DB as PostgreSQL Database

    Admin->>API: POST /api/payments/settlements { TenantId, Amount, BankReference }

    rect rgb(240, 248, 255)
        Note over API,DB: DB Transaction (Balance Mutation Serialization)
        API->>DB: BEGIN TRANSACTION
        API->>DB: SELECT * FROM LedgerAccounts<br/>WHERE TenantId = @TenantId AND Name = 'Academy Payable'<br/>FOR NO KEY UPDATE
        Note over DB: Serializes settlements against other mutations<br/>without blocking concurrent capture inserts!

        API->>DB: Compute Settleable Balance (Two-Tier Risk Formula, Q32 + Q41)
        alt Requested Amount > Settleable Balance
            Note over API: Only Settlement enforces positive settleable balance
            API-->>Admin: 422 Unprocessable Entity ("Amount exceeds settleable balance")
        end

        Note over API: Insert Settlement Record
        API->>DB: INSERT INTO Settlements (<br/>  Id = Guid.NewGuid(), TenantId = @TenantId, Amount = @Amount,<br/>  Currency = @Currency, RecordedByUserId = @AdminId, BankReference = @Ref, RecordedAt = now()<br/>)

        Note over API: Book Double-Entry Settlement
        API->>DB: INSERT INTO LedgerEntries (Type = 'Settlement', SettlementId = Settlement.Id, TenantId = @TenantId)
        API->>DB: INSERT INTO LedgerLines:<br/>  Dr Academy Payable: Amount<br/>  Cr Bank Account: Amount

        API->>DB: INSERT INTO PaymentsAuditLogs (Action = 'Settlement.Recorded')
        API->>DB: COMMIT TRANSACTION
    end

    API-->>Admin: 201 Created { SettlementId, Amount, SettleableBalanceRemaining }
```

---

## Transaction 11: Ops: Record PSP Payout (Manual)

### Overview
- **Trigger:** Ops operator records regular PSP sweep from Stripe to platform bank account (`POST /api/payments/psp-payouts`).
- **One Transaction Contains:** `PspPayout` entry + 2 lines $\rightarrow$ Audit row.
- **Idempotency:** Partial unique `(PspObjectId) WHERE Type = 'PspPayout'` stops double-recording.
- **Ledger Entries:**
  - **Dr** `Bank Account`: $\text{Amount}$
  - **Cr** `PSP Clearing`: $\text{Amount}$
  - *(TenantId = NULL, platform-level transaction)*

### Flow Diagram

```mermaid
flowchart TD
    Start(["Ops POST /api/payments/psp-payouts"]) --> Tx["Begin DB Transaction"]
    Tx --> InsertEntry["INSERT INTO LedgerEntries (<br/>  Id = Guid.NewGuid(), Type = 'PspPayout',<br/>  PspObjectId = @PspPayoutId, TenantId = NULL, PostedAt = now()<br/>)"]
    InsertEntry --> CheckUnique{"Partial unique on<br/>PspObjectId holds?"}
    CheckUnique -- No (Already Recorded) --> DuplicateError["Rollback & Return 409 Conflict"]
    CheckUnique -- Yes --> InsertLines["INSERT INTO LedgerLines:<br/>  Dr Bank Account: Amount<br/>  Cr PSP Clearing: Amount"]
    InsertLines --> Audit["INSERT INTO PaymentsAuditLogs (Action = 'PspPayout.Recorded')"]
    Audit --> Commit(["Commit Transaction"])
```

---

## Transaction 12: Courses Event Consumer: Mirror Upsert

### Overview
- **Trigger:** MassTransit consumer receives Courses catalog event (`CoursePublished`, `CourseUnpublished`, etc.).
- **One Transaction Contains:** Version-conditional upsert into `Products` $\rightarrow$ Audit row.
- **Idempotency & Ordering:** Keyed by `(TenantId, ItemType, ItemId)`. Updates only when `SourceVersion` is newer: out-of-order events become safe no-ops.

### Flow Diagram

```mermaid
flowchart TD
    Start(["Courses Integration Event"]) --> Worker["MassTransit Consumer"]
    Worker --> Tx["Begin DB Transaction"]
    Tx --> Upsert["INSERT INTO Products (<br/>  Id, TenantId, ItemType, ItemId, ParentContainerId,<br/>  TargetResourceArn, Title, IsPurchasable, SourceVersion, CreatedAt<br/>) VALUES (...)<br/>ON CONFLICT (TenantId, ItemType, ItemId) DO UPDATE<br/>SET Title = EXCLUDED.Title,<br/>    IsPurchasable = EXCLUDED.IsPurchasable,<br/>    SourceVersion = EXCLUDED.SourceVersion,<br/>    UpdatedAt = now()<br/>WHERE Products.SourceVersion < EXCLUDED.SourceVersion;"]
    Upsert --> CheckRows{"Rows Affected?"}
    CheckRows -- "0 (Stale Event)" --> AuditNoOp["Audit: Stale event ignored"]
    CheckRows -- "1 (Upserted)" --> AuditApplied["Audit: Product.Mirrored"]
    AuditNoOp --> Commit(["Commit Transaction"])
    AuditApplied --> Commit
```

---

## Transaction 13: Academy Admin: Create/Archive Price

### Overview
- **Trigger:** Academy admin updates price for a course plan (`POST /api/payments/products/{id}/prices`).
- **One Transaction Contains:** Archive superseded price $\rightarrow$ Insert new active price $\rightarrow$ Minor-unit scale trigger $\rightarrow$ Audit row.
- **Guarantees:** Handled atomically in a single transaction. Partial unique `(ProductId, Currency) WHERE IsActive = TRUE` ensures exactly one active offer.

### Flow Diagram

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Academy Admin
    participant API as Payments API
    participant DB as PostgreSQL Database

    Admin->>API: POST /api/payments/products/{id}/prices { Amount, Currency }

    rect rgb(240, 248, 255)
        Note over API,DB: Atomic Archive + Insert Transaction
        API->>DB: BEGIN TRANSACTION

        Note over API,DB: Step 1: Archive current active price
        API->>DB: UPDATE Prices<br/>SET IsActive = FALSE, ArchivedAt = now()<br/>WHERE ProductId = @ProductId AND Currency = @Currency AND IsActive = TRUE;

        Note over API,DB: Step 2: Insert replacement offer
        API->>DB: INSERT INTO Prices (<br/>  Id = Guid.NewGuid(), TenantId = @TenantId, ProductId = @ProductId,<br/>  Amount = @Amount, Currency = @Currency, IsActive = TRUE,<br/>  ArchivedAt = NULL, CreatedByUserId = @AdminId, CreatedAt = now()<br/>);
        Note over DB: BEFORE INSERT trigger verifies Amount × 10^exp is integer<br/>Partial unique (ProductId, Currency) WHERE IsActive = TRUE holds.

        API->>DB: INSERT INTO PaymentsAuditLogs (Action = 'Price.Archived')
        API->>DB: INSERT INTO PaymentsAuditLogs (Action = 'Price.Created')
        API->>DB: COMMIT TRANSACTION
    end

    API-->>Admin: 201 Created { PriceId, Amount, Currency }
```

---

## Transaction 14: Platform Admin: Settings Change

### Overview
- **Trigger:** Platform admin opts-in Academy and configures commission share (`PUT /api/payments/settings/{tenantId}`).
- **One Transaction Contains:** Upsert `AcademyPaymentSettings` (explicit `CommissionShare`) $\rightarrow$ Deterministically ensure `Academy Payable` account exists $\rightarrow$ Audit row.
- **Guarantees:** Absent settings row = selling disabled. Account creation uses `ON CONFLICT DO NOTHING` to prevent race collisions.

### Flow Diagram

```mermaid
flowchart TD
    Start(["Platform Admin PUT /settings/{tenantId}"]) --> Tx["Begin DB Transaction"]
    Tx --> UpsertSettings["UPSERT AcademyPaymentSettings (<br/>  TenantId, OnlineSellingEnabled, CommissionShare,<br/>  ReserveRate, ShortHoldDays, RefundWindowDays, UpdatedAt = now()<br/>)"]
    UpsertSettings --> EnsureAccount["INSERT INTO LedgerAccounts (<br/>  Id = gen_random_uuid(), Name = 'Academy Payable',<br/>  Type = 'Liability', Currency = @SettlementCurrency,<br/>  TenantId = @TenantId, IsActive = TRUE, CreatedAt = now()<br/>)<br/>ON CONFLICT (TenantId, Name) WHERE TenantId IS NOT NULL DO NOTHING;"]
    EnsureAccount --> Audit["INSERT INTO PaymentsAuditLogs (Action = 'Settings.Updated')"]
    Audit --> Commit(["Commit Transaction"])
```

---

## Transaction 15: Refresh Worker: `ProductDailySales` Rebuild

### Overview
- **Trigger:** Scheduled analytics projection worker.
- **One Transaction Contains:** Atomic Delete + Insert of a single `(TenantId, Day)` slice.
- **Guarantees:** Readers never see partial metrics. Bucketed in UTC: `("PaidAt" AT TIME ZONE 'UTC')::date`.

### Flow Diagram

```mermaid
flowchart TD
    Start(["Analytics Refresh Worker Cycle"]) --> Slice["Select next (TenantId, Day) slice to refresh"]
    Slice --> Tx["Begin DB Transaction"]
    Tx --> Del["DELETE FROM ProductDailySales<br/>WHERE TenantId = @TenantId AND Day = @Day;"]
    Del --> Agg["Calculate from LedgerEntries + Purchases:<br/>  - SaleCount & GrossAmount<br/>  - CommissionAmount & NetAcademyAmount<br/>  - RefundCount & RefundedGross/Commission/Net amounts"]
    Agg --> Ins["INSERT INTO ProductDailySales (<br/>  TenantId, ProductId, Day, Currency, SaleCount, GrossAmount,<br/>  CommissionAmount, NetAcademyAmount, RefundCount, RefundedAmounts,<br/>  RefreshedAt = now()<br/>)"]
    Ins --> Commit(["Commit Transaction (Slice visibly complete under MVCC)"])
```

---

## Transaction 16: Sweeper Worker: Stuck Webhook Re-Drive

### Overview
- **Trigger:** Scheduled sweeper cron for stalled or failed webhooks.
- **One Transaction Contains:** Scan non-terminal webhooks older than threshold $\rightarrow$ Re-drive through domain processor $\rightarrow$ Escalate persistent failures.
- **Guarantees:** State machine guards and row locks ensure re-drives are completely safe and idempotent.

### Flow Diagram

```mermaid
flowchart TD
    Start(["Sweeper Cron Trigger"]) --> Scan["SELECT Id FROM WebhookEvents<br/>WHERE ProcessingOutcome IS NULL<br/>AND ReceivedAt < now() - INTERVAL '5 minutes'"]
    Scan --> Loop{"For each stuck event"}
    Loop -- Next Event --> Process["Invoke Phase 2 Domain Processing<br/>(Acquires FOR NO KEY UPDATE on WebhookEvents)"]
    Process --> Guard{"State Guard<br/>already terminal?"}
    Guard -- Yes --> Skip["No-op (Already finished)"]
    Guard -- No --> ReRun["Execute Transactional State Mutations & Stamp Processed"]
    Skip --> Loop
    ReRun --> Loop
    Loop -- All Done --> CheckUnmatched["Check UnmatchedCapture entries older than N days"]
    CheckUnmatched --> Escalate["Emit Critical Alert / ReconciliationFinding"]
    Escalate --> End(["Sweeper Turn Complete"])
```

---

## Transaction 17: Reconciliation Worker

### Overview
- **Trigger:** Automated reconciliation cron (`ReconciliationWorker`).
- **One Transaction Contains:** Append row to `ReconciliationRuns` $\rightarrow$ Run audit checks $\rightarrow$ Insert any issues into `ReconciliationFindings`.
- **Kinds:** `LedgerInternal`, `LedgerVsPsp`, `MirrorVsCourses`, `StaleSessions`, `UnmatchedCaptures`, `NegativePayables`.

### Flow Diagram

```mermaid
flowchart TD
    Start(["Reconciliation Schedule Trigger"]) --> InitRun["INSERT INTO ReconciliationRuns (<br/>  Id = Guid.NewGuid(), Kind = @Kind, Status = 'Running',<br/>  TriggeredBy = 'Scheduler', StartedAt = now()<br/>)"]
    InitRun --> ExecCheck["Execute Inspection Queries:<br/>  1. Verify ΣDebits == ΣCredits across all LedgerLines<br/>  2. Compare balance_transaction reports vs LedgerEntries<br/>  3. Compare Products mirror vs Courses database<br/>  4. Identify Stale Pending CheckoutSessions<br/>  5. Identify Unresolved UnmatchedCaptures in PSP Suspense<br/>  6. Detect Negative Academy Payable balances"]
    ExecCheck --> Eval{"Discrepancies found?"}
    Eval -- Yes --> TxFindings["Open DB Transaction"]
    TxFindings --> InsertFindings["INSERT INTO ReconciliationFindings (<br/>  RunId, Severity, Kind, AggregateId, Detail<br/>)"]
    InsertFindings --> CompleteRun["UPDATE ReconciliationRuns<br/>SET Status = 'Completed', FinishedAt = now(), Counts = @Counts"]
    CompleteRun --> Commit(["Commit Transaction & Fire Ops Alert"])
    Eval -- No --> CompleteClean["UPDATE ReconciliationRuns<br/>SET Status = 'Completed', FinishedAt = now(), Counts = @Counts"]
```

---

## Bonus: Transactions 18 & 19 (Refund Requests Workflow, Q39)

For total completeness with §5 and amendment Q39, here are the student-initiated refund request flows:

```mermaid
sequenceDiagram
    autonumber
    actor Student
    actor Admin as Platform Admin
    participant API as Payments API
    participant DB as PostgreSQL Database

    rect rgb(245, 245, 255)
        Note over Student,DB: Transaction 18: Student Requests Refund
        Student->>API: POST /api/payments/refund-requests { PurchaseId, Reason }
        API->>DB: Check Purchase ownership, Paid status, no Open disputes, inside RefundWindowDays
        API->>DB: INSERT INTO RefundRequests (Id, PurchaseId, TenantId, RequestedByUserId, Reason, Status = 'Requested')
        Note over DB: Partial unique (PurchaseId) WHERE Status = 'Requested'<br/>allows at most ONE open request per purchase!
        API->>DB: INSERT INTO PaymentsAuditLogs (Action = 'Refund.Requested')
        API-->>Student: 201 Created ("Refund request submitted")
    end

    rect rgb(255, 248, 240)
        Note over Admin,DB: Transaction 19: Admin Decides Request
        Admin->>API: POST /api/payments/refund-requests/{id}/decide { Approved: true, ApprovedAmount, RevokeAccess }
        API->>DB: BEGIN TRANSACTION
        API->>DB: SELECT * FROM RefundRequests WHERE Id = @Id FOR NO KEY UPDATE
        API->>DB: SELECT * FROM Purchases WHERE Id = Request.PurchaseId FOR NO KEY UPDATE
        API->>DB: Validate ApprovedAmount <= Remaining Refundable (Q41)

        alt Approved
            API->>DB: INSERT INTO Refunds (<br/>  Id = Guid.NewGuid(), PurchaseId = Purchase.Id, Amount = ApprovedAmount,<br/>  Source = 'Platform', Status = 'Pending', RevokeAccess = RevokeAccess,<br/>  RequestedByUserId = Admin.Id, ReasonCode = 'RequestedByCustomer'<br/>)
            API->>DB: UPDATE RefundRequests SET Status = 'Approved', ApprovedAmount = @ApprovedAmount,<br/>  DecidedByUserId = @AdminId, DecidedAt = now(), RefundId = Refund.Id
            API->>DB: INSERT INTO PaymentsAuditLogs (Action = 'Refund.Approved')
            Note over API: Handed off to Transaction 4's outbound PSP call path!
        else Rejected
            API->>DB: UPDATE RefundRequests SET Status = 'Rejected', DecidedByUserId = @AdminId, DecidedAt = now()
            API->>DB: INSERT INTO PaymentsAuditLogs (Action = 'Refund.Rejected')
        end
        API->>DB: COMMIT TRANSACTION
        API-->>Admin: 200 OK
    end
```

---

## ⚖️ Ledger Entries Summary & T-Accounts Reference

| Transaction & Entry Type | Debits (Dr) | Credits (Cr) | Reference Constraints |
|:---|:---|:---|:---|
| **Capture** (Tx 2) | **PSP Clearing** ($G$)<br/>**PSP Fees** ($F$, if $F > 0$) | **Academy Payable** ($G - \text{ROUND}(G \times c)$)<br/>**Platform Revenue** ($\text{ROUND}(G \times c)$)<br/>**PSP Clearing** ($F$, if $F > 0$) | `PurchaseId` NOT NULL, others NULL. Partial unique on `PurchaseId`. |
| **RefundReversal** (Tx 5) | **Academy Payable** ($\text{Amount} - \text{CommissionLeg}$)<br/>**Platform Revenue** ($\text{CommissionLeg}$) | **PSP Clearing** ($\text{Amount} - \text{PspFeeRefunded}$)<br/>**PSP Fees** ($\text{PspFeeRefunded}$, if $>0$) | `RefundId` & `PurchaseId` NOT NULL. Partial unique on `RefundId`. |
| **DisputeLoss** (Tx 7) | **Academy Payable** ($\text{DisputeAmount} - \text{PlatformLeg}$)<br/>**Platform Revenue** ($\text{PlatformLeg}$)<br/>**PSP Fees** ($\text{PspDisputeFee}$) | **PSP Clearing** ($\text{DisputeAmount} + \text{PspDisputeFee}$) | `DisputeId` & `PurchaseId` NOT NULL. Partial unique `(DisputeId, Type)`. |
| **DisputeWon** (Tx 8) | **PSP Clearing** ($\text{DisputeAmount}$) | **Academy Payable** ($\text{AcademyLeg}$)<br/>**Platform Revenue** ($\text{PlatformLeg}$) | `DisputeId` NOT NULL. Partial unique `(DisputeId, Type)`. |
| **Settlement** (Tx 10) | **Academy Payable** ($\text{Amount}$) | **Bank Account** ($\text{Amount}$) | `SettlementId` NOT NULL. Partial unique on `SettlementId`. |
| **PspPayout** (Tx 11) | **Bank Account** ($\text{Amount}$) | **PSP Clearing** ($\text{Amount}$) | `PspObjectId` NOT NULL, `TenantId` NULL. Partial unique on `PspObjectId`. |
| **UnmatchedCapture** (Tx 2 fallback) | **PSP Clearing** ($G$) | **PSP Suspense** ($G$) | `PspObjectId` & `Description` NOT NULL. |
