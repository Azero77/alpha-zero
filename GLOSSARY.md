# AlphaZero Learning Academy

A multi-tenant learning platform: academies host courses, and students buy access to them either with access codes (offline economy) or with online card purchases. This glossary is the shared language across the platform's modules.

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
An online card purchase by a student that unlocks access exactly as a redeemed Access Code does.
_Avoid_: sale, order, payment

**Purchase Activation**:
The event that turns a completed Purchase into a student's access grant, through the same pipeline a redeemed code follows.

**Course Plan**:
A purchasable tier of a course: it names the tier and defines the access window (no window means indefinite access).

**Price Catalog**:
The payments-module-owned record of what each sellable costs.
_Avoid_: price book, product table, pricing table

### Money

**Ledger**:
The append-only, double-entry record of every monetary event on the platform; balances are derived from it and entries are never edited or deleted.
_Avoid_: transactions table, wallet

**Balance Owed**:
An Academy's accrued, not-yet-settled share of Purchases, derived from the Ledger.

**Settlement**:
A recorded transfer of Balance Owed (or part of it) from the Platform to an Academy, settled outside the online payment provider.
_Avoid_: payout, withdrawal

**Commission Share**:
The Platform's percentage of each Purchase, configurable per Academy; the remainder is the Academy's share. Processing fees are borne by the Platform's share.

**Refund**:
A full return of a Purchase to the student; it revokes the access the Purchase granted.

**Dispute**:
A chargeback raised against a Purchase; recorded and surfaced in the dashboard, resolved by people.
