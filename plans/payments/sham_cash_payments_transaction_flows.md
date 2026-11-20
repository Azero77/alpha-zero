# Payments — ShamCash Wire Contract and Flow Deltas

**Status:** Provider-specific source, synchronized 2026-11-18 with Q39–Q58.

**Ownership:** [`transaction_flows.md`](transaction_flows.md) owns shared checkout, webhook, Unapplied Funds, review, settlement, access, outbox, lock, and Ledger workflows. [`database.md`](database.md) owns data invariants. This file owns only ShamCash capabilities, wire contracts, normalization, and deltas. [`design.md`](design.md) owns implementation boundaries.

Provider identity: `PaymentProviders.Name = 'sham-cash'`. MVP uses one dedicated, Platform-owned, AlphaZero-exclusive linked account with SYP→SYP and USD→USD routes, `PresentmentMode = Exact`.

---

## 1. Capability matrix

| Capability | ShamCash MVP behavior |
|---|---|
| Hosted checkout | Hosted exact invoice |
| Customer identity | No customer mirror; payer account is not Ledger identity |
| Presentment | Exact Price amount/currency; no provider FX |
| Ordinary refund | Unsupported; Student is directed to support |
| Dispute lifecycle | Unsupported; never create a `Disputes` row |
| Incoming truth | Signed callbacks plus invoice/transaction/balance reconciliation |
| Outgoing truth | Confirmed transfer Provider Transaction |
| Settlement source | Same-currency ShamCash Wallet is allowed |
| Risk gate | Successful daily reconciliation covering captures; no rolling reserve after that |
| Account management | Provider dashboard only; app monitors health |

ShamCash implements checkout, event-normalization, and reconciliation capabilities. It does not implement ordinary refund or dispute capabilities.

---

## 2. Hosted invoice contract

Create the invoice outside a database transaction with:

- `hosted = true`;
- `amount` and `currency` exactly equal to the locked Price;
- `order_id = CheckoutSessionId`;
- `Idempotency-Key = CheckoutSessionId`;
- unpredictable, never-reused `payment_note`;
- 30-minute expiry.

Persist the invoice ID as `ExternalCheckoutId`. Return the hosted URL to the Student but do not persist it.

Only configured exact SYP or USD routes are eligible. EUR or another published provider currency remains disabled until fixture, sandbox, Ledger-account, and reconciliation evidence is approved.

Timeout is ambiguous. Reuse the local pending session and idempotency key; retrieve the invoice before creating anything else.

---

## 3. Signature and delivery identity

- Verify `X-ShamCash-Signature` over exact raw bytes using the provider's timestamped HMAC-SHA256 contract.
- Require `X-ShamCash-Event-ID` and use it as delivery `ExternalEventId`.
- Reject missing, malformed, stale, or invalid signatures before any database write.
- Invoice and transaction callback families may report the same transfer under different Event IDs. They are separate deliveries but one movement.
- Ingest event, hot payload, and `ProcessWebhookEvent` outbox message as specified by [`transaction_flows.md`](transaction_flows.md#4-webhook-ingest-and-processing).

The sandbox smoke must verify the published header names, canonical signing string, timestamp tolerance, retry behavior, and payload schemas before implementation is release-ready.

---

## 4. Event and movement normalization

Normalize callback/history data to:

- linked Provider Account;
- numeric provider `transaction_id` as `ExternalTransactionId`;
- invoice ID as `ExternalPaymentId`;
- invoice/order/note references;
- incoming/outgoing direction;
- positive amount and exact currency;
- occurred time and provider status.

`UNIQUE (ProviderAccountId, ExternalTransactionId)` is the crediting authority. Callback, invoice status, transaction history, and manual evidence describing the same movement converge on that identity. A duplicate with different immutable fields is Critical.

---

## 5. Exactness delta

An incoming transfer auto-activates only when all conditions hold:

1. `order_id` or invoice relationship resolves the locked Checkout Session;
2. amount equals locked Price amount;
3. currency equals locked Price and route currency;
4. provider movement occurred inside `[invoice.CreatedAt, invoice.ExpiresAt)`;
5. movement is not already recorded;
6. Provider Account/route remains the session's environment and partition.

A transfer inside the window may activate when observed later by callback, polling, or reconciliation. Observation time is not payment time.

Any partial, overpaid, duplicate-note, currency-mismatched, after-window, or unmatched incoming transfer follows the canonical Unapplied Funds path. It creates no Purchase/access and never enters Academy Payable until approved application.

---

## 6. Polling and reconciliation reads

Webhooks are notification, not sole truth.

Outside database transactions, the reconciler reads:

1. account balance per enabled currency;
2. transactions since `watermark − overlapWindow`;
3. invoice status for stale pending sessions.

Respect the published rate limit (currently documented as 6 requests/minute), back off on `429`, and verify this limit in sandbox before freezing worker cadence.

For each movement, normalize and run the same shared command used by callbacks. Never insert a Purchase/Ledger entry directly from the worker. Persist a `LedgerVsProvider` run and findings for missing/extra/mismatched movements, balance drift, stale invoices, or account health.

A genuinely unpaid invoice becomes expired after provider confirmation. An after-window payment remains Unapplied Funds even when polling discovers it.

---

## 7. Review and reversal deltas

### Unapplied receipts

- Academy operator may open an evidence-backed case to apply the exact expected amount or return all/part of remaining Unapplied Funds.
- A different Platform-finance actor approves.
- Approval alone changes no balance. Application requires Purchase eligibility; return requires confirmed outgoing Provider Transaction.
- Overpayment application credits only expected amount to Academy Payable/Platform Revenue. Excess remains Unapplied until separately returned.

### Purchase reversal

- The normal Refund Request endpoint rejects ShamCash Purchases with a support instruction.
- ShamCash has no refund API and no formal dispute event family.
- An exceptional provider-support reversal requires a reviewed case plus confirmed outgoing/reversal Provider Transaction.
- Resolution creates a succeeded `Refund(Source = ProviderSupport)` only to reuse the shared reversal, Purchase status, and access-revocation behavior.
- An email, support ticket, or approval without movement evidence cannot mark money returned.

---

## 8. Settlement delta

ShamCash Academy liabilities may settle directly from the same-currency ShamCash Wallet:

1. maker/checker case identifies exact payable account, amount, destination snapshot, and evidence;
2. approved transfer runs outside the database transaction;
3. confirmed outgoing `transaction_id` becomes the Provider Transaction;
4. shared result phase recomputes Settleable Balance and posts Dr Academy Payable / Cr ShamCash Wallet.

Settlement is blocked until a successful daily `LedgerVsProvider` run with `FinishedAt` after the relevant capture covers that movement. Route policy then uses `ShortHoldDays = 0`, `ReserveRate = 0`, and `DisputeHoldDays = 0` because ordinary refunds/disputes are unsupported. Unapplied Funds remains outside Academy Payable.

---

## 9. Provider health delta

Signals include:

- invalid or rotated credentials;
- expired linked-account session/subscription;
- signature failure spike;
- invoice/transaction callback lag;
- reconciliation/balance drift;
- disabled currency route.

Mark only the ShamCash Provider Account or route degraded/disabled. Stop new ShamCash checkout. Keep existing callback ingest, polling, reconciliation, review, settlement repair, and immutable history active. Stripe and other provider routes remain unaffected.

The account is AlphaZero-exclusive. An unrelated incoming movement is still real cash: ingest it into Unapplied Funds and page operations rather than ignoring it.

---

## 10. Idempotency evidence

| Layer | Key |
|---|---|
| Invoice call | `CheckoutSessionId` in header and `order_id` |
| Delivery | (`sham-cash`, `X-ShamCash-Event-ID`) |
| Movement | (`ProviderAccountId`, `transaction_id`) |
| Attempt | pending (`UserId`, `ProductId`) |
| Purchase | unique Checkout Session and Provider Transaction |
| Ledger effect | business reference plus (`ProviderTransactionId`, entry type) |

Different callback families or reconciliation reads may win different races. The resulting domain state must be identical.

---

## 11. Go-live and fixture matrix

Release evidence must cover:

- invoice creation, idempotent retry, retrieve, expiry, and hosted URL handling;
- valid, missing, malformed, and stale signatures;
- exact SYP and USD receipt;
- partial, overpaid, duplicate, currency-mismatched, late, and unmatched receipt;
- invoice callback + transaction callback + history overlap for one `transaction_id`;
- polling an in-window transfer observed after invoice expiry time;
- applying expected overpayment while leaving excess unapplied;
- returning Unapplied Funds with outgoing movement evidence;
- unsupported ordinary Refund Request and absence of Dispute rows;
- support-confirmed reversal with maker/checker and movement proof;
- settlement before/after daily reconciliation;
- credential/subscription degradation and recovery;
- transaction-history watermark overlap, rate limiting, and balance drift.

Deterministic signed fixtures run in PR CI. A secret-gated sandbox smoke must prove the published contract before the ShamCash slice is release-ready. Missing credentials yields **BLOCKED**, not passed.

All shared concurrency, Ledger, outbox, access, archive, analytics, and review tests live under the canonical flows/design, not in this provider document.
