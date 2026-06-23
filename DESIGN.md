# Design System — AlphaZero Learning Academy

## Product Context
- **What this is:** Tenant-Based SaaS e-learning platform ("Shopify For Schools")
- **Who it's for:** Schools, universities, and their students in the MENA region.
- **Space/industry:** High-performance, localized ed-tech.
- **Project type:** Web App (Teacher/Admin Dashboard) & Mobile App (Student Hub)

## Aesthetic Direction
- **Direction:** Minimal / Utilitarian (with premium execution)
- **Decoration level:** Minimal (relying on typography and stark borders, not heavy shadows)
- **Mood:** Serious, fast, and professional. It feels like high-end enterprise software, not a toy.

## Typography
- **Display/Hero:** Outfit — Geometric and precise, giving a premium feel without being overly decorative.
- **Body:** Outfit — Highly legible for dense interfaces.
- **Arabic Display/Body:** Cairo — Contemporary Kufic roots, pairs flawlessly with Outfit, ensures culturally native localization.
- **Data/Tables/Code/Math:** JetBrains Mono — Excellent tabular number alignment and clear LaTeX representation.
- **Loading:** Next/font optimization for web, bundled assets for React Native.
- **Scale:** 
  - 2xl: 48px
  - xl: 32px
  - lg: 24px
  - md: 16px (Base)
  - sm: 14px
  - xs: 12px

## Color
- **Approach:** Restrained, High-Contrast
- **Primary:** `#111827` (Near-Black) — Primary text and heavy borders.
- **Secondary:** `#FFFFFF` — Surface backgrounds.
- **Background:** `#FAFAFA` — Page background to reduce eye strain.
- **Muted/Border:** `#E5E7EB` (Borders), `#6B7280` (Muted text).
- **Accent (Tenant):** `#0F172A` (Default Slate-Black) — Premium and unobtrusive.
- **Semantic:** success `#10B981`, warning `#F59E0B`, error `#EF4444`, info `#3B82F6`
- **Dark mode:** Surfaces `#1E293B`, Background `#0F172A`, Borders `#334155`, Text `#F8FAFC`.

## Spacing
- **Base unit:** 8px
- **Density:** Compact for dashboards, comfortable for student-facing courses.
- **Scale:** 2xs(2px) xs(4px) sm(8px) md(16px) lg(24px) xl(32px) 2xl(48px)

## Layout
- **Approach:** Grid-disciplined
- **Grid:** 12-column web, flexible grid mobile.
- **Max content width:** 1200px (Web)
- **Border radius:** sm(4px) for controls, md(8px) for major cards, full(9999px) for badges/avatars.

## Motion
- **Approach:** Minimal-functional
- **Easing:** ease-out (enter), ease-in (exit)
- **Duration:** micro(150ms) for hovers, short(250ms) for modal reveals.
- **Delight:** Specialized confetti effect reserved exclusively for physical library code redemption.

## Decisions Log
| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-06-19 | Initial design system created | Created by /design-consultation to optimize for low-bandwidth MENA users with a premium "Shopify-level" polish. |
