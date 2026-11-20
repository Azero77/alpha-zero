# Project Intake Brief

**Phase:** Phase 1 — Intake Interview  
**Project:** resilient-academy  
**Date:** 2026-11-18  
**Confidence:** Medium-High  

---

## 1. Project Background & Working Title

* **Working Title:** Resilient Academy (formerly *AlphaZero Learning Academy* — discarded due to brand saturation, trademark friction with DeepMind, and lack of domain fit).
* **Core Concept:** A high-performance, white-labeled e-learning and academy management platform engineered specifically for low-connectivity and cash-constrained markets (starting with Syria/Levant). It enables private tutoring institutes and star teachers to deliver protected online courses, grade handwritten assessments, and monetize through both digital local rails (Sham Cash) and physical library cash codes.

---

## 2. Problem & Solution Definition

### The Problem
* **Unbanked & Sanctioned Market:** In Syria and parts of the Levant, students and academies cannot use international credit cards (Visa, Mastercard, Stripe, PayPal) to purchase or sell digital educational content.
* **Informal & Chaotic Alternatives:** Teachers currently distribute course materials using Telegram channels, Google Drive links, and WhatsApp groups. 
  * *Zero Piracy Protection:* Videos are downloaded, forwarded, screen-recorded, and re-sold on torrents/Telegram black markets.
  * *Zero Student Analytics:* No completion tracking, attendance, or verifiable progress.
  * *Administrative Burden:* Manual money collection, manual verification of receipts, and chaotic grading.
* **Bandwidth & Infrastructure Deficits:** Intermittent electricity and throttled internet make heavy video streaming unusable without adaptive bitrates, aggressive compression, and offline caching.

### The Proposed Solution
* **Multi-Tenant White-Labeled Academy:** Private academies and high-profile teachers get a branded academy portal and a unified mobile client.
* **Hybrid Cash / Local Digital Payments:**
  1. *Physical Library Code Economy:* Students walk into local partner stationery shops and libraries, pay local cash, and receive a single-use scratch/activation code that instantly unlocks course access.
  2. *Sham Cash Gateway:* Direct electronic payment integration for students with access to Syria's local digital wallet.
* **Ultra-Low Cost, Protected Video Pipeline:**
  * Adaptive HLS video streaming transcoded via custom FFmpeg on AWS Lambda.
  * Zero-egress bandwidth delivery via Cloudflare R2 and Cloudflare CDN.
  * DRM protection (ClearKey initially with low-friction path to full Widevine/FairPlay DRM) plus context-aware device binding (limiting student logins to registered devices).
* **Pedagogical Integrity:**
  * Support for handwritten homework/exam image uploads with a dedicated teacher grading UI.
  * Lesson-specific discussions and verified answers.

---

## 3. Target Customer & Beachhead Segment

* **Beachhead Customer:** Independent star teachers (e.g., Grade 9 / Baccalaureate / University level) and mid-sized private tutoring academies in major Syrian metropolitan centers (Damascus, Aleppo, Homs, Latakia).
* **Value Proposition to Teachers/Academies:** "Protect your intellectual property from Telegram piracy, automate student enrollment, and get paid via Sham Cash and library cash networks without upfront software costs."
* **End Users:** Middle school, high school (Baccalaureate), and university students requiring reliable supplemental education.

---

## 4. Business & Monetization Model

* **Revenue Model:** Commission / Take-Rate model.
  * The platform charges a transaction fee (e.g., 10%–20%) on every course enrollment sold through physical library codes or Sham Cash.
  * Academies and teachers pay $0 upfront setup costs, eliminating adoption friction.
* **Financial Ledger Architecture:** Built-in multi-tenant double-entry accounting ledger tracking gross sales, library distributor cuts, platform commissions, and teacher net payouts.

---

## 5. Founder Context & Technical Stack

* **Founder:** Solo technical founder.
* **Background:** Senior backend software engineer specializing in C# / .NET.
* **Budget / Runway:** $0 external funding (pure bootstrapping).
* **Architecture Stack:**
  * *Backend:* C# .NET (Clean Architecture / FastEndpoints / PostgreSQL with Bitmasking for progress tracking).
  * *Frontend:* React for web portal; planned single unified mobile client (React Native / Flutter) where students load their specific academy.
  * *Media Infrastructure:* AWS S3 (input ingestion) -> FFmpeg on AWS Lambda (transcoding) -> Cloudflare R2 (storage with zero egress fees) -> Cloudflare CDN (global delivery).
  * *DRM & Security:* ClearKey / Widevine pipeline with device fingerprint enforcement.

---

## 6. Critical Unknowns, Blind Spots & Kill Criteria

* **The Piracy Trap:** Teachers expect 100% piracy guarantees. In reality, the "analog hole" (recording the phone screen with an external camera) cannot be stopped by software alone; the platform must provide forensic watermarking (showing student phone numbers on screen) and device locking rather than promising mathematically impossible piracy immunity.
* **Client App Bottleneck:** As a solo backend developer with $0 budget, building web, admin, grading, and a cross-platform mobile client simultaneously is the greatest execution risk. The initial MVP must strictly minimize client scope (e.g., responsive React web app + lightweight PWA first, or single unified mobile client).
* **Kill Criteria (Conditions to Walk Away):**
  1. Teachers insisting on 100% piracy immunity and refusing to adopt if any screen recording or analog workaround is theoretically possible.
  2. Failure to recruit active students or partner libraries willing to stock and sell code vouchers.
  3. Regulatory or operational blockage preventing Sham Cash or cash ledger payouts.

---

## 7. Naming & Brand Constraints

* **Preferences:**
  * Language: English, abstract, global tech feel.
  * Inspirations: *Stripe, Notion, Linear, Vercel, Canvas*.
  * Persona: Elite institutional authority blended with accessible, resilient grassroots empowerment.
  * Discarded: *AlphaZero* (AI research collision, overused, irrelevant).

---

## Flags

**Red Flags:**
- Solo technical founder with $0 capital attempting to build backend, admin dashboard, teacher grading interface, and cross-platform mobile apps simultaneously. Scope must be aggressively pruned to survive.

**Yellow Flags:**
- Teacher validation to date consists of informal "thumbs up" without prepaid commitments or signed exclusive distribution agreements.
- Local currency inflation in Syria requires all course pricing and code ledger entries to be pegged to stable purchasing power units rather than fixed paper amounts.

## Sources
- Founder Intake Interview (Rounds 1–3, 2026-11-18)
- Architecture PRD (`docs/PRD.md`)
