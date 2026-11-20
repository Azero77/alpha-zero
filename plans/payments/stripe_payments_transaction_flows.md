# Payments — Stripe Wire Contract and Flow Deltas

**Status:** Provider-specific source, synchronized 2026-11-18 with Q39–Q58.

**Ownership:** [`requirement.md`](requirement.md) owns product behavior. [`database.md`](database.md) owns schema and invariants. [`transaction_flows.md`](transaction_flows.md) owns shared checkout, webhook, refund, dispute, settlement, access, outbox, lock, and Ledger workflows. This file owns only Stripe capabilities, wire contracts, normalization, and deltas. [`design.md`](design.md) owns implementation boundaries; repository-root [`GLOSSARY.md`](../../GLOSSARY.md) owns terminology.

Provider identity: `PaymentProviders.Name = 'stripe'`. MVP configures one Platform-owned account per enabled environment, initially with a USD integration/settlement route and `PresentmentMode = Adaptive`.

---

## 1. Capability matrix

| Capability | Stripe MVP behavior |
|---|---|
| Hosted checkout | Checkout Sessions, one-time payment, instant capture only |
| Student identity | Inline one-time checkout data; no durable provider-customer mirror |
| Price identity | Inline price data from the locked immutable Price; no provider-price mirror |
| Presentment | Adaptive Pricing may localize Student amount/currency |
| Settlement truth | Integration amount/currency plus expanded balance transaction |
| Refund | Partial/full, platform- or provider-originated, asynchronous |
| Dispute | Open, funds withdrawn/reinstated, closed |
| Provider payout | Balance movement to provider-neutral Bank account |
| Reconciliation | Balance transactions/reports, payouts, refunds, disputes, provider balance |

Stripe implements `ICheckoutProvider`, `IRefundProvider`, `IProviderEventNormalizer`, and `IProviderReconciler`. It does not define shared application sequencing.

---

## 2. Checkout create contract

The checkout orchestrator supplies:

- provider idempotency key: `CheckoutSessionId`;
- `client_reference_id = CheckoutSessionId`;
- metadata: `TenantId`, `UserId`, `ProductId`, `PriceId`, `CheckoutSessionId`;
- mode: one-time payment;
- inline price amount/currency from the locked Price;
- Adaptive Pricing enabled only on a route validated to support it;
- configured success/cancel return URLs;
- instant capture; delayed capture is outside MVP.

The adapter returns the Stripe Checkout Session ID and hosted URL. Persist only the external ID and non-secret normalized metadata; return the URL to the caller.

Timeout is ambiguous. The orchestrator keeps the local session pending and retries/retrieves with the same idempotency root.

---

## 3. Signature and delivery identity

- Verify Stripe's signature over the exact raw request bytes, including timestamp tolerance, before any write.
- Use Stripe's immutable Event ID as `ExternalEventId`.
- Stamp `Environment` from Stripe's livemode/test indicator and require it to match the configured Provider Account.
- A valid Test event cannot resolve or mutate a Live Checkout Session, Provider Transaction, Purchase, Refund, Dispute, or Ledger partition.
- Ingest event, hot payload, and `ProcessWebhookEvent` outbox message as specified by [`transaction_flows.md`](transaction_flows.md#4-webhook-ingest-and-processing).

---

## 4. Event normalization

| Stripe signal | Normalized fact | Shared workflow |
|---|---|---|
| `checkout.session.completed` | checkout completed; fetch settlement truth | exact payment |
| `checkout.session.expired` | definitive session expiry | session terminal transition |
| `refund.created` | refund known/pending or provider-originated | Refund upsert |
| `refund.updated` | refund status changed | Refund confirmation/failure |
| `refund.failed` | definitive refund failure | Refund failed, no Ledger write |
| legacy `charge.refunded` / `charge.refund.updated` where delivered | map to the same normalized refund facts | same Refund workflow |
| `charge.dispute.created` | dispute opened | insert `Open`, no money write |
| `charge.dispute.funds_withdrawn` | outgoing dispute movement | `DisputeLoss` posting |
| `charge.dispute.funds_reinstated` | incoming reinstatement movement | `DisputeWon` posting |
| `charge.dispute.closed` | paperwork outcome | close and conditional revoke; no money posting |
| payout/balance movement signal or report row | provider payout movement | Provider payout posting/reconciliation |

Event names are adapter inputs, never Domain enum values. Fixture refresh must verify the current Stripe API version and payload shapes before implementation.

---

## 5. Capture truth and Adaptive Pricing

On `checkout.session.completed`, retrieve the Checkout Session/PaymentIntent with the relevant expanded balance transaction outside any database transaction.

The normalized capture must contain:

- Provider Account/environment;
- Stripe balance-transaction ID as `ExternalTransactionId`;
- PaymentIntent/charge IDs as external references;
- integration/settlement gross and currency;
- provider fee and net;
- occurred time;
- Student presentment amount/currency from `presentment_details`, when present;
- effective presentment-to-settlement rate when calculable.

Validation:

1. `client_reference_id`/metadata resolve the local Checkout Session first; external ID is fallback evidence.
2. Integration amount/currency must equal the locked Price/route contract.
3. `payment_status = unpaid` never creates a Purchase.
4. Commission and analytics use settlement gross. Stripe's Student-paid Adaptive Pricing markup never enters the Ledger.
5. Provider Transaction dedupe uses the Stripe balance-transaction ID within the Provider Account, not Event ID, PaymentIntent, or Checkout Session ID.

---

## 6. Refund deltas

- Stripe supports ordinary Refund Requests and direct Platform refund issuance.
- Provider request idempotency key is `RefundId`; metadata includes `RefundId` and `PurchaseId`.
- A provider call accepting a refund does not itself post the Ledger reversal. Confirmed Provider Transaction movement does.
- `refund.created` may introduce a dashboard-originated refund absent locally; create `Refund(Source = Provider)` and reserve it as external truth. If it raced local capacity, allow negative Remaining Refundable, block new local reservations, and raise a Critical finding.
- If an already-confirmed dashboard Refund raced a local reservation, normalize and book the external movement once, allow negative refundable/payable state, and raise a Critical reconciliation finding; never omit provider cash to preserve a local cap.
- Ambiguous API outcomes remain pending. Only definitive provider rejection or `refund.failed` marks failed.
- Actual movement supplies settlement amount/currency and any returned fee. The shared workflow reverses the stored Commission Share pro rata.
- Stripe may refund the Student in presentment currency while the Ledger records actual settlement movement in the Provider Account currency.

The adapter supports the approved partial/full refund contract.

---

## 7. Dispute deltas

- Only formal Stripe dispute events create `Disputes` rows.
- `charge.dispute.created` opens the case but does not imply cash timing.
- Funds-withdrawn/reinstated events normalize to immutable Provider Transactions and drive Ledger postings immediately.
- `charge.dispute.closed` records `Won`/`Lost`; it never duplicates money movement.
- Partial dispute amount is legal. Allocation reversal/restoration is pro rata from the Purchase's stored Commission Share.
- A lost dispute revokes access regardless of amount. A won dispute drops from Remaining Refundable after funds are reinstated.

---

## 8. Payout, settlement, and risk deltas

- Stripe provider payout moves money from Stripe Clearing to a provider-neutral same-currency Bank account. It is not an Academy Settlement.
- Academy Settlement normally credits Bank after provider payout; it still debits one exact Stripe Academy Payable account.
- Route defaults: 14-day 100% short hold, then 10% rolling reserve through day 120. Sparse Academy overrides may be stricter only.
- Pending Refunds, open Refund Requests, and active dispute exposure reduce Settleable Balance as defined in [`database.md`](database.md).

---

## 9. Reconciliation reads

Outside database transactions, fetch overlapping windows of Stripe balance transactions/reports plus refund, dispute, payout, and balance data needed to prove every normalized movement.

For each row:

1. normalize to Provider Account, external movement ID, type, direction, amount, currency, fee, and occurred time;
2. insert/reuse Provider Transaction;
3. compare immutable fields on duplicate; disagreement is Critical, never last-write-wins;
4. verify expected semantic Ledger posting exists once;
5. persist counts/findings in a `LedgerVsProvider` run.

Reconciliation may confirm a missed webhook but may not bypass the shared Application command or Ledger posting boundary.

---

## 10. Provider health and go-live

Mark only the Stripe Provider Account/route degraded when credentials fail, environment mismatches, contracts drift, or reconciliation exceeds tolerance. New Stripe checkout stops; existing webhook ingest, refunds, disputes, reconciliation, and history remain active.

Stripe Live is release-ready only when:

- account eligibility and compliance are approved;
- credentials/webhook secret are in KeyVault and environment isolation tests pass;
- deterministic signed fixtures pass in PR CI;
- secret-gated Stripe test-mode smoke proves create/retrieve, signature verification, idempotency, capture normalization, refund, event normalization, and reconciliation;
- monitoring, sweeper, archive, restore rehearsal, and legal gates pass.

Missing credentials yields **BLOCKED**, not passed.

---

## 11. Stripe-specific fixture matrix

- Checkout completed with same settlement/presentment currency.
- Adaptive Pricing with different presentment amount/currency.
- Unpaid/expired Checkout Session.
- Duplicate delivery and different Event IDs referencing the same balance transaction.
- Platform partial refund, provider-originated refund, async failure, and retry after timeout.
- Dispute open → withdrawal → won/reinstated → close; open → withdrawal → lost → close.
- Provider payout and report overlap.
- Test event targeting Live local state.
- Signature missing, stale, malformed, and valid.

All shared concurrency, Ledger, outbox, access, archive, settlement, and analytics tests live under the canonical flows/design, not in this provider document.
