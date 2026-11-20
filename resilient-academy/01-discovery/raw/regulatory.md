# Regulatory, Payment & Sanctions Landscape

**Phase:** Phase 3 — Wave 1: Market Landscape (Raw Research)  
**Project:** resilient-academy (Working Title: Plexor)  
**Date:** 2026-11-18  
**Confidence:** Medium-High  

---

## 1. Local Financial Regulations & Payment Rails (Syria)

### Sham Cash & Central Bank of Syria (CBS) Directives
* **Regulation:** Electronic payment providers in Syria operate under strict supervision of the Central Bank of Syria (CBS) to curb unauthorized liquidity speculation and informal forex trading.
* **Cash-Out Commissions:** CBS imposes strict commission ceilings on electronic wallet cash-outs (e.g. 0.3% max commission for authorized money agents).
* **Compliance for Educational Platforms:** Platforms integrating Sham Cash or Syriatel/MTN Cash function as registered merchants (*Tajer*). Settlements must comply with local commercial registry requirements if operating formally as an enterprise entity.
* **Physical Library Code Legality:** Selling paper software licenses/scratch-cards at retail stationery stores and libraries is categorized under standard retail retail goods/stationery sales. No special financial services license is required as long as the codes are single-use prepaid product vouchers (not circulating currency or stored value).

---

## 2. International Sanctions & Cloud Infrastructure Compliance (OFAC / US / EU)

* **Current Status:** The geopolitical environment surrounding Syria has undergone rapid shifts, including broad U.S. OFAC General Licenses (such as GL 23/25 and internet services exemptions under 31 CFR § 542.511) designed to permit software, educational tools, and internet communications services for civilian populations.
* **Over-blocking & "Compliance Lag":** US tech vendors (AWS, Cloudflare, Google Play, Apple) still have automated geographic blocking filters that occasionally flag traffic or developer registrations originating from Syrian IP addresses.
* **Architectural Mitigation:**
  * *Hosting Location:* Run cloud backend and storage in European or regional neutral data center regions (e.g., Frankfurt, Istanbul, Bahrain).
  * *Dual-Distribution Strategy:* Distribute the Android application both through Google Play Store (for diaspora/VPN users) and direct APK download via the web portal (for local Syrian students whose devices lack access to Play Services).
  * *Segregation of Revenue:* Cloud infrastructure bills (AWS/Cloudflare) must be settled via foreign accounts/cards, while Syrian student revenues are settled locally in Syrian currency via Sham Cash and physical cash collection.

---

## Flags

**Red Flags:**
- Attempting to route Syrian student payments directly into foreign bank accounts or international payment processors (Stripe/PayPal) is legally prohibited under remaining banking restrictions.

**Yellow Flags:**
- Automated risk filters on Apple App Store or Google Play can cause delayed app reviews if Syrian educational terminology or local payment methods are explicitly referenced in English metadata.

## Sources
- US Department of the Treasury / OFAC General Licenses (31 CFR § 542)
- Central Bank of Syria (CBS) Electronic Payment Directives (2025/2026)
