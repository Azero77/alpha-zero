# AlphaZero Learning Academy

A multi-tenant learning platform: academies host courses, and students buy access either with Access Codes (offline economy) or supported online payment providers. This glossary is the shared language across the platform's modules.

## Language

### Platform & Tenancy

**Platform**:
AlphaZero itself — the SaaS operator that hosts academies. The seller of record for every online purchase and the recipient of the commission share.
_Avoid_: the SaaS, the system

**Academy**:
One academy hosted on the platform. The codebase models this as a Tenant.
_Avoid_: organization, workspace, school

**Student**:
A learner who pays for and consumes access to academy resources.
_Avoid_: user, customer, buyer

**Library**:
A physical retail branch that sells access codes over the counter.
_Avoid_: store, branch

### Access & Selling

**Access Code**:
A single-use code that grants one student access to one course plan when redeemed.
_Avoid_: voucher, coupon, key, license

**Redemption**:
The act of a student activating an access code on their own account.

**Sale**:
The offline record of an access code sold at a Library; the money never touches the platform.
_Avoid_: purchase, payment

**Purchase**:
A confirmed online acquisition by a Student, funded by one exact provider movement and granting access through Courses' source-aware access capability.
_Avoid_: sale, checkout attempt, payment

**Purchase Activation**:
The event that asks Courses to grant access for a completed Purchase through its shared, source-aware access capability.

**Course Plan**:
A purchasable tier of a course: it names the tier and defines the access window (no window means indefinite access).

**Price Catalog**:
The payments-module-owned record of what each sellable costs.
_Avoid_: price book, product table, pricing table

**Product**:
A Payments-owned mirror of something sellable; in the MVP, one Course Plan. Courses changes reach it through versioned events.
_Avoid_: course, catalog row

**Price**:
An immutable amount and currency offered for one Product. Changing a Price archives it and creates another.
_Avoid_: Course Plan, provider price

**Checkout Session**:
A Student's pending or completed provider attempt, locking one Product, Price, and Provider Currency Route. Only confirmed money produces a Purchase.
_Avoid_: purchase, order, cart

**Sellable**:
A Product with an active compatible Price, offered only while its Academy and at least one Provider Currency Route are enabled.
_Avoid_: SKU, offering

**Eligibility**:
The business decision whether a Student may start checkout for a Product. Active course access rejects a new attempt; financial history alone does not.
_Avoid_: database uniqueness, payment status

### Money

**Ledger**:
The append-only, double-entry record of every monetary event on the platform; balances are derived from it and entries are never edited or deleted.
_Avoid_: transactions table, wallet

**Balance Owed**:
An Academy's accrued, not-yet-settled share of Purchases for one Provider Account and currency, derived from the Ledger.

**Settlement**:
A confirmed transfer reducing one Academy's payable for one Provider Account and currency. The source may be a Platform bank account or a provider wallet, depending on policy.
_Avoid_: provider payout, withdrawal

**Commission Share**:
The Platform's percentage of each Purchase, configurable per Academy; the remainder is the Academy's share. Processing fees are borne by the Platform's share.

**Provider**:
A payment integration identified by an immutable machine name such as `stripe` or `sham-cash`. Enabling a database row does not install adapter code.
_Avoid_: provider account, gateway when identity matters

**Provider Account**:
One Platform-owned external account for one Provider and environment. It partitions movements, routes, Ledger accounts, reconciliation, and health.
_Avoid_: Academy account, Ledger account

**Provider Currency Route**:
An enabled integration/settlement currency and presentment mode for one Provider Account. It determines method eligibility, not price.
_Avoid_: Price, Academy route

**Payment Method**:
A Student-facing checkout choice, such as card or exact transfer, derived from an eligible Provider Currency Route.
_Avoid_: Provider Currency Route, Provider Account

**Provider Transaction**:
The immutable normalized record of an external money movement, unique within its Provider Account.
_Avoid_: webhook event, Ledger entry

**Presentment Currency**:
The currency the Student was charged. It may differ from Stripe's settlement currency under Adaptive Pricing.
_Avoid_: reporting currency

**Settlement Currency**:
The currency of one Provider Account's Ledger and analytics partition.
_Avoid_: global base currency

**Reporting Currency**:
The Settlement Currency of one Provider Account analytics partition. MVP has no normalized cross-provider/cross-currency reporting total.
_Avoid_: global base currency

**Capture**:
The confirmed incoming provider movement that creates a Purchase, allocates its split in the Ledger, and requests access.
_Avoid_: checkout completion page, Sale

**Reversal**:
An append-only Ledger effect that returns or withdraws some/all of a Purchase through a Refund or lost Dispute.
_Avoid_: rollback, edited capture

**Refund Request**:
A Student's request for money back, reviewed by Platform finance. Approval chooses an amount and creates a Refund; the request itself moves no money.
_Avoid_: refund, dispute

**Refund**:
A partial or full reversal of a Purchase. A Pending Refund reserves refundable amount; a Succeeded Refund confirms provider movement; a Failed Refund releases that reservation. Revocation is explicit per Refund, except a fully refunded Purchase always loses access.
_Avoid_: refund request, dispute

**Remaining Refundable**:
Purchase settlement gross minus Pending/Succeeded Refunds and Dispute Exposure currently withdrawn and not reinstated.
_Avoid_: settleable balance, unrefunded amount

**Dispute**:
A formal provider chargeback against a Purchase. Opening is paperwork and does not imply cash moved; cash is booked when withdrawn or reinstated, while closing records Won or Lost. Only providers with dispute capability create one.

**Dispute Exposure**:
The disputed amount currently withdrawn and not reinstated. It reduces Remaining Refundable independently from whether the Dispute paperwork is still Open or has closed Lost.
_Avoid_: open dispute amount

**Settleable Balance**:
The amount that may be transferred from one exact Academy payable after Pending Refunds, open Refund Requests, risk reserves, and reconciliation requirements are applied.
_Avoid_: available balance, Balance Owed

**Unapplied Funds**:
A provider-scoped liability for incoming money that cannot yet become a Purchase, such as a partial, excess, duplicate, late, or unmatched receipt.
_Avoid_: failed payment

**Payment Review Case**:
An evidence-backed maker/checker workflow for applying or returning Unapplied Funds, exceptional provider-support reversals, adjustments, or Settlements.
_Avoid_: status override, support ticket
