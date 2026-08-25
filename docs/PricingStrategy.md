# 💰 AlphaZero — Pricing & Monetization Strategy

> **Status:** Active  
> **Last Updated:** 2026-08-24  
> **Decision:** Hybrid Model — Platform Fee + Revenue Share  
> **Online Payments:** Study phase — implementation deferred until first tenants provide feedback  
> **Related Docs:** [PRD](./PRD.md) · [Architecture Strategy](./Architecture-Strategy.md) · [Payments Integration Plan](./PaymentsIntegrationPlan.md) · [Brand Strategy](./AlphaZero-Brand-Strategy.md)

---

## 1. Monetization Model Overview

AlphaZero earns revenue from two independent streams that serve different sides of the platform:

```
┌──────────────────────────────────────────────────────────────────────┐
│  STREAM 1: B2B Platform Fee (School → AlphaZero)                    │
│  Fixed monthly/annual subscription for platform access               │
│  Covers: hosting, subdomain, branding, infrastructure, support       │
│  Collected: Monthly invoice (manual initially, automated later)      │
├──────────────────────────────────────────────────────────────────────┤
│  STREAM 2: Revenue Share (% of Course Sales → AlphaZero)            │
│  Percentage cut on every student course purchase                     │
│  Applies to: Library code sales AND future online payments           │
│  Collected: Built into wholesale code pricing (library codes)        │
│             Automated deduction (online payments, when implemented)  │
└──────────────────────────────────────────────────────────────────────┘
```

**Why Hybrid?** A fixed fee alone underprices successful schools. A revenue share alone makes revenue unpredictable. The combination gives AlphaZero a cost floor (the fixed fee covers infrastructure) and an upside ceiling that scales with tenant success.

---

## 2. Platform Fee — Tier Structure

### Proposed Tiers

| Tier | Student Limit | Monthly Price | Annual Price | Includes |
|------|---------------|---------------|--------------|----------|
| **Starter** | ≤ 200 | $49 | $470 (20% off) | 1 subdomain, basic branding (logo + 2 colors), 3 staff accounts, 50 GB video storage |
| **Academy** | ≤ 1,000 | $149 | $1,430 (20% off) | Full branding (custom theme), unlimited staff, 500 GB video storage, library code management |
| **University** | ≤ 5,000 | $399 | $3,830 (20% off) | Everything in Academy + priority support, API access, advanced analytics, 2 TB video storage |
| **Enterprise** | Unlimited | Custom | Custom | Dedicated infrastructure, SLA, custom development, on-premises option |

### Tier Design Rationale

- **Student limits are soft caps**, not hard walls. A school exceeding 200 students gets a friendly upgrade prompt, not a locked-out classroom. Never punish a school for growing.
- **Annual pricing offers 20% discount** — standard SaaS convention. Schools budget annually, so this aligns with their procurement cycle.
- **Starter at $49/mo** is deliberately low. The goal is adoption, not margin. At $1.30/student infrastructure cost, 200 students costs AlphaZero ~$21/month. The $49 covers costs with room to spare.
- **Video storage limits** map directly to Cloudflare R2 costs (essentially $0 egress, ~$0.015/GB/mo storage). 50 GB costs AlphaZero $0.75/month. These limits exist for positioning, not cost pressure.

### MENA-Adjusted Pricing (Syria Focus)

For Syrian institutions, pricing should reflect local purchasing power:

| Tier | Suggested SYP Equivalent | USD Equivalent |
|------|--------------------------|----------------|
| **Starter** | Negotiated per school | $20–30/mo |
| **Academy** | Negotiated per school | $60–100/mo |

> **Rule:** For the first 5 tenants, pricing is negotiable. The goal is product validation, not revenue optimization. A school paying $20/month and giving honest feedback is worth more than one paying $149/month and churning silently.

---

## 3. Revenue Share — Course Sales

### Rate

| Revenue Share Tier | Rate | Applies When |
|--------------------|------|-------------|
| **Standard** | **10%** of gross course sale price | Default for all tenants |
| **Early Adopter** | **5%** for first 12 months | First 5 tenants (incentive) |
| **Volume** | **8%** | Tenants exceeding $10,000/month in course sales |

### How Revenue Share Works — By Payment Channel

#### Channel 1: Library Code Sales (Current — Fully Operational)

Library codes use a **wholesale pricing model** to capture AlphaZero's revenue share at the point of code generation, before any physical sale happens:

```
Course retail price:        $20 (set by school)
AlphaZero revenue share:    $20 × 10% = $2.00
Library retail margin:      $20 × 10% = $2.00
School wholesale price:     $20 - $2.00 (AlphaZero) - $2.00 (Library) = $16.00

Money flow:
  Student pays library:     $20 cash
  Library keeps:            $2.00 (retail margin)
  Library remits to school: $18.00
  School remits to AZ:      $2.00 (revenue share)
  School keeps:             $16.00

  OR (preferred — simpler):

  School buys codes from AlphaZero at $18.00 each (retail - AZ share)
  School sells codes to library at $18.00 each
  Library sells to student at $20.00
  Library keeps $2.00 margin
  AlphaZero already collected $2.00 per code at generation time
```

**Collection mechanism:** AlphaZero's share is baked into the wholesale code price. When a school requests code generation, the system calculates:
- `WholesalePrice = RetailPrice × (1 - PlatformShareRate)`
- The school pays AlphaZero the wholesale price per code batch
- AlphaZero's revenue share is pre-collected, eliminating post-sale settlement

**Tracking:** Every code redemption is logged in the `RedemptionAuditLog` with `TenantId`, `LibraryId`, `AccessCodeId`, timestamp, and device fingerprint. Monthly settlement reports can be generated from this data.

#### Channel 2: Online Payments (Future — Post-Tenant-Feedback)

When online payments are implemented, revenue share collection is automated:

```
Course retail price:         $20 (set by school)
Payment processor fee:       $20 × 2.9% + $0.30 = $0.88
AlphaZero revenue share:     $20 × 10% = $2.00
School receives:             $20 - $0.88 - $2.00 = $17.12

Money flow:
  Student pays via Stripe:    $20
  Stripe takes:               $0.88 (processing fee)
  AlphaZero takes:            $2.00 (revenue share)
  School receives:            $17.12 (net)
```

**Collection mechanism:** Stripe Connect or manual payout. AlphaZero receives the full payment, deducts its share, and disburses the remainder to the school's configured bank account.

---

## 4. Revenue Distribution Summary

### Per-Sale Breakdown (All Channels)

```
                        Library Code     Online Payment    Online (Syria
                        (Current)        (Stripe/Intl)     Local Gateway)
                        ─────────────    ──────────────    ──────────────
Payment Processor       0%               2.9% + $0.30     TBD (est. 3-5%)
AlphaZero Share         10%              10%               10%
Library Margin          ~10%             N/A               N/A
School Net Revenue      ~80%             ~87%              ~85%
```

### Monthly Revenue Projection (Per School)

| Scenario | Students | Sales/mo | Avg Price | Platform Fee | Rev Share | AlphaZero Total |
|----------|----------|----------|-----------|-------------|-----------|-----------------|
| Small school | 100 | 50 | $15 | $49 | $75 | **$124/mo** |
| Medium school | 500 | 200 | $20 | $149 | $400 | **$549/mo** |
| Large university | 2,000 | 800 | $25 | $399 | $2,000 | **$2,399/mo** |

### Platform-Wide Revenue Targets

| Milestone | Tenants | Avg Revenue/Tenant | Monthly Revenue |
|-----------|---------|-------------------|-----------------|
| **Survival** | 3 | $200 | $600/mo |
| **Sustainability** | 10 | $400 | $4,000/mo |
| **Growth** | 25 | $500 | $12,500/mo |
| **Scale** | 50 | $600 | $30,000/mo |

---

## 5. Online Payments — Study & Implementation Roadmap

### Current Status: STUDY PHASE

Online payments are architecturally designed ([PaymentsIntegrationPlan.md](./PaymentsIntegrationPlan.md)) but **not implemented**. This is intentional. The priority is:

1. Launch with library codes (the market's native payment method)
2. Onboard 2–3 tenants and collect real usage data
3. Let tenant feedback drive the payment feature priority

### Phase Gates — What Triggers Each Phase

```
PHASE 0: LIBRARY CODES ONLY (Current → Launch + 3 months)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Status:     ✅ Fully implemented
Trigger:    N/A — this is the launch state
Scope:      - Physical code generation, distribution, redemption
            - RedemptionAuditLog for all transactions
            - Manual invoicing for platform fees (PDF/email)
Exit Gate:  Move to Phase 1 when ANY of these are true:
            □ A tenant explicitly requests online payment
            □ An international school (non-Syria) wants to onboard
            □ 3+ tenants are live and providing positive feedback
            □ A student demographic survey shows >20% would prefer online


PHASE 1: STRIPE CHECKOUT (Trigger-based → ~2-3 days to build)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Status:     📋 Architecturally planned (PaymentsIntegrationPlan.md)
Trigger:    Phase 0 exit gate met
Scope:      - Stripe Checkout hosted payment page (no PCI burden)
            - Webhook handler with idempotency (IdempotentEvent table)
            - Reuse existing CourseRedemptionSaga
            - PaymentTransaction audit ledger
            - CoursePrice and Coupon domain models
Effort:     ~75 minutes with CC (T1-T4 in PaymentsIntegrationPlan.md)
NOT scope:  - Custom payment forms (use Stripe's hosted page)
            - Subscription billing for schools (still manual)
            - Syrian local gateways (Phase 2)
Exit Gate:  Move to Phase 2 when ANY of these are true:
            □ Syrian tenants report students wanting local mobile payment
            □ Syria's e-payment regulations mature enough for integration
            □ A licensed local gateway (ecash) offers API access


PHASE 2: SYRIAN LOCAL GATEWAYS (Market-driven → weeks to build)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Status:     🔍 Studying — Central Bank Decision No. 1124 (Aug 2026)
            creates legal framework; ecash is licensed for QR/wallets
Trigger:    Phase 1 exit gate met
Scope:      - Integrate 1-2 licensed Syrian payment providers
            - Same webhook pattern as Stripe (gateway-agnostic design)
            - SYP currency support per tenant configuration
            - Mobile wallet QR code flow
Candidates: ecash, Syriatel Cash, Sham Cash, Al-Haram Exchange
Research:   Study API docs, licensing requirements, and settlement
            cycles for each candidate during Phase 0-1
NOT scope:  - Building our own payment gateway
            - Dynamic forex conversion (hardcoded rates per tenant)
Exit Gate:  Move to Phase 3 when:
            □ 10+ tenants are active
            □ Manual invoicing becomes a bottleneck (>2 hrs/week)


PHASE 3: TENANT BILLING AUTOMATION (Scale-driven)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Status:     📋 Not designed yet
Trigger:    Phase 2 exit gate met
Scope:      - Stripe Billing for recurring tenant platform fees
            - Hosted Customer Portal (self-service plan management)
            - Automated invoicing with usage-based revenue share
            - Tenant Admin billing dashboard
            - Revenue share settlement reports
Effort:     TBD — design when approaching 10 tenants
```

### Study Tasks (Do Now — Phase 0)

These are research tasks to prepare for online payments without writing production code:

- [ ] **S1** — Read Stripe Checkout and Stripe Connect documentation
  - Goal: Understand hosted checkout, webhook lifecycle, and marketplace payouts
  - Output: Notes on which Stripe features map to our architecture
  - Link: [stripe.com/docs/checkout](https://stripe.com/docs/checkout)

- [ ] **S2** — Research Syria's Central Bank Decision No. 1124
  - Goal: Understand the regulatory framework for e-payments in Syria
  - Output: Summary of licensing requirements, allowed providers, sandbox rules
  - Note: The "regulatory sandbox" provision may allow testing before full licensing

- [ ] **S3** — Survey licensed Syrian payment providers
  - Goal: Identify which providers have APIs, what currencies they support, settlement cycles
  - Candidates: ecash (QR + wallets), Syriatel Cash, Sham Cash
  - Output: Comparison table of API availability, fees, and integration complexity

- [ ] **S4** — Design tenant feedback collection mechanism
  - Goal: Build a simple way to ask tenants about payment preferences
  - Options: In-app survey, Google Form link in Tenant Admin dashboard, direct WhatsApp
  - Questions to ask:
    1. "Do your students ask about paying online? How often?"
    2. "What payment methods do your students currently use outside of library codes?"
    3. "Would you pay more for AlphaZero if it included online payment processing?"
    4. "What's your biggest pain point with the library code system?"

- [ ] **S5** — Validate wholesale pricing model with first tenant
  - Goal: Confirm the revenue share collection mechanism works in practice
  - Test: Generate a batch of codes, track the wholesale price flow, verify audit log
  - Output: Confirmation that `RedemptionAuditLog` provides sufficient data for settlement

---

## 6. Library Code Economics — Detailed Model

### Code Lifecycle & Revenue Capture

```
┌─────────────┐     ┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│   MINTED    │────▶│ DISTRIBUTED │────▶│   REDEEMED   │     │   VOIDED    │
│             │     │             │     │              │     │             │
│ Code hashed │     │ Assigned to │     │ Student uses │     │ Cancelled / │
│ in DB       │     │ library     │     │ code, course │     │ expired     │
│             │     │ branch      │     │ unlocked     │     │             │
│ Revenue:    │     │ Revenue:    │     │ Revenue:     │     │ Revenue:    │
│ AZ share    │     │ No change   │     │ Confirmed    │     │ Refund if   │
│ collected   │     │             │     │ in audit log │     │ pre-paid    │
│ at this     │     │             │     │              │     │             │
│ step        │     │             │     │              │     │             │
└─────────────┘     └─────────────┘     └──────────────┘     └─────────────┘
```

**Key insight:** AlphaZero's revenue share is collected at **mint time** (when the school purchases the code batch), not at redemption time. This means:
- Zero settlement delay — AlphaZero is already paid
- No dependency on student behavior — revenue is locked in at generation
- Voided codes may require refund processing (handle manually for MVP)

### Pricing Example — Batch of 100 Codes

```
Course:              "Physics 101" by Damascus University
Retail price:        $20 per code
Revenue share:       10%

Calculation:
  AZ share per code: $20 × 10% = $2.00
  Wholesale per code: $20 - $2.00 = $18.00

School pays AlphaZero:
  100 codes × $18.00 = $1,800.00 (wholesale batch price)

AlphaZero revenue:
  100 codes × $2.00 = $200.00 (pre-collected revenue share)

School distributes to library at wholesale:
  Library buys 100 codes at $18.00 each = $1,800.00
  Library sells at $20.00 each = $2,000.00
  Library margin: $200.00 (10%)

School net from this batch:
  Wholesale received from library: $1,800.00
  Paid to AlphaZero: $1,800.00 (already paid at mint)
  School net: $0 from library sales
  BUT: School set the retail price. The school's revenue comes from
       choosing a retail price that covers their content creation costs
       plus the wholesale price they paid AlphaZero.

CORRECTED MODEL (School keeps margin):
  School sets internal cost:  $12 (content creation + overhead)
  School sets retail price:   $20
  AZ share at mint:           $2.00 per code
  School pays AZ:             $2.00 per code (AZ share only)
  School gives library:       codes at $18.00 wholesale
  Library sells at:           $20.00 retail
  Library remits to school:   $18.00 per sale

  Per code sold:
    Student pays:       $20.00
    Library keeps:      $2.00
    School receives:    $18.00
    School pays AZ:     $2.00 (already paid at mint)
    School net:         $16.00
    AZ net:             $2.00
```

---

## 7. Financial Reporting — What to Build & When

### Phase 0 (Now — Library Codes Only)

| Report | For Whom | Data Source | Format |
|--------|----------|-------------|--------|
| Code Batch Summary | Tenant Admin | `AccessCode` table | In-app dashboard (existing) |
| Redemption Log | Tenant Admin | `RedemptionAuditLog` | In-app (existing) |
| Monthly Settlement | AlphaZero (you) | Manual export from DB | Spreadsheet |
| Platform Fee Invoice | Tenant Admin | Manual | PDF via email |

### Phase 1+ (After Online Payments)

| Report | For Whom | Data Source | Format |
|--------|----------|-------------|--------|
| Transaction History | Tenant Admin | `PaymentTransaction` | In-app dashboard |
| Revenue Share Statement | Tenant Admin | Aggregated from all channels | Monthly PDF |
| Payout Summary | Tenant Admin | Stripe Connect / manual | In-app + email |
| Platform Revenue Dashboard | AlphaZero (you) | All sources | Internal admin panel |

---

## 8. Competitive Positioning — Pricing Comparison

| Platform | Model | Typical Cost (500 students) | School Keeps |
|----------|-------|----------------------------|-------------|
| **Coursera for Campus** | Per-learner license | $400–600/yr per learner ($200K+) | N/A (content is Coursera's) |
| **Teachable** | Monthly + 5% transaction fee | $99/mo + 5% of sales | ~90% |
| **Thinkific** | Monthly (no transaction fee) | $149/mo | 100% (but no marketplace) |
| **Udemy Business** | Per-seat annual license | $30/user/yr ($15K) | 37% (Udemy marketplace) |
| **Custom LMS (Moodle)** | Self-hosted | $5K–50K setup + hosting | 100% (but you build everything) |
| **AlphaZero** | $49–149/mo + 10% rev share | $49–149/mo + 10% of sales | **~80–87%** |

**AlphaZero's pitch:**
> "You keep 80–87% of every sale. You own your brand. You reach students on 3G. And you don't need $50,000 to start — you need $49/month."

---

## 9. Risks & Mitigations

| Risk | Severity | Mitigation |
|------|----------|------------|
| Schools resist 10% revenue share | High | Early Adopter rate (5% for 12 months) + transparent reporting showing value delivered |
| Library code wholesale model creates cash flow friction | Medium | Offer credit terms for first batch; settle monthly after trust is established |
| Online payment regulations in Syria change | Low | Gateway-agnostic architecture (PaymentsIntegrationPlan.md already designed for this) |
| Schools underreport library code sales | Medium | All codes are system-generated and tracked — no off-platform minting is possible |
| Currency volatility (SYP) | High | Price in USD, accept SYP at monthly-updated rate; defer dynamic forex |
| Stripe not available in Syria | Low (for Syrian students) | Library codes are the primary channel; Stripe serves international students only |

---

## 10. Decision Log

| ID | Decision | Date | Rationale |
|----|----------|------|-----------|
| P1 | Hybrid model (Platform Fee + Revenue Share) | 2026-08-24 | Balances predictable revenue with growth-aligned upside |
| P2 | 10% standard revenue share | 2026-08-24 | Sweet spot — meaningful for AZ, painless for schools (5% early adopter) |
| P3 | Defer online payments until tenant feedback | 2026-08-24 | Library codes are the market's native payment method; prove product first |
| P4 | Wholesale pricing for revenue share collection | 2026-08-24 | Pre-collects AZ share at code mint, no post-sale settlement needed |
| P5 | Manual invoicing for platform fees until 10+ tenants | 2026-08-24 | Don't build billing automation before you have customers to bill |

---

## Appendix: Tenant Feedback Template

Use this template when collecting payment feedback from early tenants (Study Task S4):

```
AlphaZero Payment Experience Survey
────────────────────────────────────

1. How many library codes has your institution sold this month? ____

2. What percentage of your students ask about paying online?
   □ 0%  □ 1-10%  □ 11-25%  □ 26-50%  □ 50%+

3. Which online payment methods would your students use? (check all)
   □ Credit/Debit card (Visa/MC)
   □ Syriatel Cash
   □ Sham Cash (MTN)
   □ ecash
   □ Bank transfer
   □ Other: ____________

4. What's your biggest challenge with library code distribution?
   □ Students can't find a library near them
   □ Library stock runs out
   □ Students want to buy at night/weekends (library closed)
   □ Code entry is confusing
   □ Other: ____________

5. Would you pay a higher platform fee if online payments were included?
   □ Yes, definitely
   □ Maybe, depends on the cost
   □ No, library codes are sufficient

6. Any other feedback on the payment experience?
   ___________________________________________________
```
