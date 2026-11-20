# Pre-Flight Check

**Phase:** Phase 0.5 — Pre-Flight Sanity Check  
**Project:** resilient-academy  
**Date:** 2026-11-18  
**Confidence:** Medium  

---

## Executive Summary

Before investing time into full strategy, market research, and product planning, a fast pre-flight check was conducted across three disqualifying vectors: dominant solution dominance, precedent failures, and regulatory/sanctions barriers.

### 1. Dominant Solution Check
* **Market Landscape:** Regional MENA EdTech giants exist (Abwaab raised ~$25M+, Noon Academy raised ~$40M+, Baims, Nagwa, Classera). However, their operational model is built around digital credit card payments, high-speed streaming, and formal school contracts in stable economies (Saudi Arabia, Jordan, Egypt, UAE).
* **Syria / Fragmented Levant Gap:** In Syria, formal EdTech incumbents do not operate due to financial sanctions and low consumer credit card penetration. The primary competition is informal, zero-cost ad-hoc tools: **Telegram channels, Google Drive folders, and in-person tutoring halls**. 
* **Verdict:** ✅ **No dominant incumbent found in the specific target wedge.** The space for an integrated white-labeled academy solution combining physical library codes, Sham Cash, and low-bandwidth video delivery remains open.

### 2. Precedent Failure Check
* **Emerging Markets Physical Code/Voucher Models:** EdTech and telecom providers in Sub-Saharan Africa and South Asia historically used scratch-card PIN vouchers to monetize unbanked users.
* **Failure Modes Identified:**
  1. *Supply Chain & Shrinkage:* High physical card printing and distribution costs, theft by retail intermediaries, and counterfeit serials.
  2. *Currency Devaluation:* In hyper-inflationary environments (such as Syria), fixed-price physical cards lose value between printing and student redemption unless tied dynamically to floating currency units.
  3. *Distributor Hold-up:* Retail distributors (stationery stores/libraries) demanding high margins (15–30%) or failing to remit collected cash promptly.
* **Verdict:** ⚠️ **Operational caution required.** Physical distribution requires strict ledger reconciliation, dynamic digital code generation, and low distribution friction.

### 3. Regulatory, Sanctions & Financial Blockers
* **Sanctions & Export Controls:** Hosting infrastructure targeting Syria is subject to US (OFAC) and EU export control regulations. Standard cloud vendors (AWS, Cloudflare) have complex compliance requirements.
* **Payment Rails:** International payment gateways (Stripe, PayPal) are completely unavailable in Syria. Local payment integration (**Sham Cash**) combined with cash redemption via physical partner libraries is the only viable settlement mechanism.
* **Verdict:** ⚠️ **Regulatory compliance risk.** Infrastructure deployment must be architected with regional/sanction-resilient cloud postures, and local cash flows must remain physically and legally isolated from foreign cloud vendor billings.

---

## Flags

**Red Flags:**
- None identified that are immediately fatal at the intake stage.

**Yellow Flags:**
- Currency volatility and inflation in Syria can rapidly erode fixed-price scratch card margins unless indexed.
- Teacher demands for "100% piracy-proof" streaming are technically impossible (analog hole, screen capture); customer education and device binding are mandatory to manage expectations.

## Sources
- [Data, World Bank & Arab News 2025/2026] MENA EdTech funding and market maturity reports (Tier 2)
- [Data, Edutech Global 2025] Emerging market voucher and offline learning models (Tier 2)
- [Data, Syrian Electronic Payment Reports 2025/2026] Sham Cash adoption and retail cash dynamics (Tier 2)
