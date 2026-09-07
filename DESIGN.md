# AlphaZero — Design System & Style Reference
> high-performance precision SaaS e-learning platform

**Themes:** `dark` (command center default) & `light` (high-clarity study mode)

AlphaZero is an enterprise-grade, multi-tenant SaaS e-learning platform optimized for low-bandwidth environments (Syria/MENA) using a unique physical library code economy. The design system is a precision-machined learning instrument built on pure neutral Zinc foundations with a crisp, accessible **Precision Blue** primary interactive scale (`#2563eb`). The interface pairs high typographic density with comfortable long-form readability: crisp high-contrast type, subtle 0.5px hairline borders, compact 4px spacing rhythm, and purposeful micro-interactions. Dark mode provides a focused nighttime study command center, while Light mode delivers high-clarity textbook legibility without eye fatigue.

---

## Tokens — Colors

### 1. Core Production Foundations (Pure Zinc Neutrals)
Neutral foundations provide a zero-chroma, distraction-free substrate for prolonged reading and video learning.

| Token | Dark Value | Light Value | OKLCH (Dark / Light) | CSS Variable | Semantic Role |
|-------|------------|-------------|----------------------|--------------|---------------|
| **Void** | `#09090b` | `#ffffff` | `oklch(0.145 0 0)` / `oklch(1 0 0)` | `--color-void`, `--background` | Page canvas, full-bleed backgrounds |
| **Carbon** | `#121215` | `#f9fafb` | `oklch(0.18 0 0)` / `oklch(0.985 0 0)` | `--color-carbon`, `--card` | Card surfaces, course cards, navigation bars |
| **Obsidian** | `#18181b` | `#f4f4f5` | `oklch(0.21 0 0)` / `oklch(0.96 0 0)` | `--color-obsidian`, `--popover` | Elevated panels, dialog sheets, modal drawers |
| **Surface-Tint** | `#222226` | `#ececee` | `oklch(0.25 0 0)` / `oklch(0.93 0 0)` | `--color-surface-tint`, `--accent` | Hover backgrounds, subtle selection states |
| **Graphite** | `#27272a` | `#e4e4e7` | `oklch(0.27 0 0)` / `oklch(0.92 0 0)` | `--color-graphite`, `--border` | Subtle hairline borders, ghost button outlines, dividers |
| **Smoke** | `#3f3f46` | `#d4d4d8` | `oklch(0.35 0 0)` / `oklch(0.86 0 0)` | `--color-smoke`, `--input` | Interactive borders, form input outlines, separators |
| **Ash** | `#71717a` | `#71717a` | `oklch(0.55 0 0)` / `oklch(0.55 0 0)` | `--color-ash`, `--muted-foreground` | Muted body copy, inactive icons, secondary metadata |
| **Fog** | `#a1a1aa` | `#52525b` | `oklch(0.71 0 0)` / `oklch(0.44 0 0)` | `--color-fog` | Tertiary metadata, placeholder text, lesson index numbers |
| **Mist** | `#e4e4e7` | `#27272a` | `oklch(0.92 0 0)` / `oklch(0.27 0 0)` | `--color-mist`, `--foreground` | Secondary headings, body text, button text on dark surfaces |
| **Paper** | `#ffffff` | `#09090b` | `oklch(1 0 0)` / `oklch(0.145 0 0)` | `--color-paper` | Primary headings, hero display type, max-contrast emphasis |

### 2. Interactive Scale (Precision Blue)
Shades of blue signal actionable, clickable affordances across the entire LMS. Precision Blue (`#2563eb`) serves as the primary visual flashlight.

| Name | Hex Value | OKLCH | Token | Role |
|------|-----------|-------|-------|------|
| **Blue Subtle** | `#eff6ff` (Light) / `rgba(37,99,235,0.12)` (Dark) | `oklch(0.97 0.02 260)` | `--color-blue-subtle` | Active lesson highlight in syllabus, selected table row, pill background |
| **Blue Light** | `#bfdbfe` | `oklch(0.88 0.08 260)` | `--color-blue-light` | Hover border for interactive cards, secondary badge text |
| **Precision Blue (Primary)** | `#2563eb` | `oklch(0.55 0.22 260)` | `--color-precision-blue`, `--primary` | Primary action buttons ("Enroll", "Next Lesson", "Redeem Code", "Submit Quiz") |
| **Blue Hover** | `#1d4ed8` | `oklch(0.48 0.22 260)` | `--color-blue-hover` | Hover state for primary action buttons |
| **Blue Active** | `#1e40af` | `oklch(0.42 0.20 260)` | `--color-blue-active` | Pressed/active state for primary action buttons |
| **Blue Focus Ring** | `rgba(37, 99, 235, 0.5)` | `oklch(0.55 0.22 260 / 0.5)` | `--color-blue-ring`, `--ring` | 3px focus ring for keyboard navigation and input focus |

### 3. Functional LMS Status & Accent Tokens
Unlike purely decorative palettes, LMS workflows require strict status semantics for grades, lesson progression, and voucher validity.

| Name | Dark Hex | Light Hex | Token | Role |
|------|----------|-----------|-------|------|
| **Success Green** | `#22c55e` | `#16a34a` | `--color-success` | Completed lesson checkmark, quiz passed (≥80%), voucher successfully activated |
| **Destructive Red** | `#ef4444` | `#dc2626` | `--color-destructive` | Quiz failed, invalid voucher PIN, session device-lock warning |
| **Warning Amber** | `#f59e0b` | `#d97706` | `--color-warning` | Voucher expiring soon, assignment due date alert, low-bandwidth warning |
| **Signal Teal** | `#38bdf8` | `#0284c7` | `--color-signal-teal` | Offline sync indicator, cached video indicator, informational banners |
| **Iris Violet** | `#818cf8` | `#6366f1` | `--color-iris-violet` | Subject track tags (Mathematics, Computer Science, Languages) |

---

## Tokens — Typography

### Inter Variable — Primary UI, headings, and reading typeface · `--font-inter-variable`
- **Substitute:** Inter, or system-ui as fallback
- **Weights:** 300 (Light), 400 (Regular), 510 (Medium-Display), 590 (Semibold-Heading)
- **Sizes:** 10, 11, 12, 13, 14, 15, 16, 17, 20, 24, 32, 48, 64, 72
- **Line height:** 1.0–1.7 (1.5–1.6 for long-form lesson reading)
- **Letter spacing:** -0.022em at 48–72px, -0.012em at 20–32px, -0.010em at 13–16px
- **OpenType features:** `"cv01" on, "ss03" on, "zero" on`
- **Role:** Primary UI, course titles, navigation links, lesson body text, buttons

### Berkeley Mono — Code, Lesson Indices, and Physical Library Vouchers · `--font-berkeley-mono`
- **Substitute:** JetBrains Mono, IBM Plex Mono, or ui-monospace
- **Weights:** 400, 500
- **Sizes:** 12, 14, 16
- **Letter spacing:** 0.05em on voucher PIN inputs, -0.013em on metadata
- **OpenType features:** `"cv01" on, "ss03" on`
- **Role:** Code exercises, lesson sequence counter (`04/24`), duration timestamps (`08:42`), and physical Library Code vouchers (`AZ-9482-K92X`)

### Type Scale

| Role | Size | Line Height | Letter Spacing | Token | Context |
|------|------|-------------|----------------|-------|---------|
| caption | 13px | 1.2 | normal | `--text-caption` | Timestamps, author subtext, lesson count |
| body-sm | 15px | 1.6 | -0.165px | `--text-body-sm` | Course descriptions, sidebar lesson tree |
| body-lg | 20px | 1.4 | -0.24px | `--text-body-lg` | Course lead paragraphs, reading summary |
| subheading | 24px | 1.33 | -0.288px | `--text-subheading` | Module titles, section headers |
| heading-sm | 32px | 1.13 | -0.704px | `--text-heading-sm` | Course landing page titles, dashboard greeting |
| heading | 48px | 1.05 | -1.056px | `--text-heading` | Academy marketing titles, major milestones |
| heading-lg | 64px | 1.0 | -1.408px | `--text-heading-lg` | Marketing hero display type |
| display | 72px | 1.0 | -1.584px | `--text-display` | Promotional hero numbers, completion certificate badge |

---

## Tokens — Spacing & Shapes

**Base unit:** 4px  
**Density:** Compact in dashboard navigation; comfortable in lesson reading and quiz interfaces.

### Spacing Scale

| Name | Value | Token |
|------|-------|-------|
| 4 | 4px | `--spacing-4` |
| 8 | 8px | `--spacing-8` |
| 12 | 12px | `--spacing-12` |
| 16 | 16px | `--spacing-16` |
| 20 | 20px | `--spacing-20` |
| 24 | 24px | `--spacing-24` |
| 32 | 32px | `--spacing-32` |
| 48 | 48px | `--spacing-48` |
| 64 | 64px | `--spacing-64` |
| 96 | 96px | `--spacing-96` |

### Border Radius

| Element | Value | Purpose |
|---------|-------|---------|
| badges | 4px | Status tags, lesson index badges |
| inputs / buttons | 6px | Precision buttons, PIN input blocks, form fields |
| cards | 12px | Course cards, video player frame, modal dialogues |
| pills | 9999px | Category filters, user avatar borders, active tab pills |

### Shadows & Hairline Elevation
Elevation is achieved via subtle 0.5px–1px hairline borders (`--color-graphite`) combined with low-opacity dark ambient shadows. No floating heavy colored glows.

| Name | Value | Token |
|------|-------|-------|
| sm | `rgba(0, 0, 0, 0.25) 0px 1px 3px 0px` | `--shadow-sm` |
| md | `rgba(0, 0, 0, 0.3) 0px 4px 12px 0px` | `--shadow-md` |
| subtle | `var(--color-graphite) 0px 0px 0px 1px inset` | `--shadow-subtle` |
| xl | `rgba(0, 0, 0, 0.5) 0px 8px 32px 0px` | `--shadow-xl` |

---

## Components

### 1. Primary Action Button (Precision Blue)
**Role:** High-emphasis primary CTA — "Enroll in Academy", "Next Lesson", "Submit Quiz", "Redeem Code"
- **Background:** `#2563eb` (`--color-precision-blue`)
- **Hover / Active:** `#1d4ed8` / `#1e40af`
- **Text:** `#ffffff` (`--color-paper` in dark / white)
- **Border-radius:** 6px
- **Padding:** 10px 16px
- **Typography:** Inter 14px / weight 510, letter-spacing -0.011em
- **Focus ring:** 3px `rgba(37, 99, 235, 0.5)` with 2px offset

### 2. Secondary / Ghost Outline Button
**Role:** Secondary actions — "Preview Syllabus", "Download Offline Audio", "Previous Lesson"
- **Background:** Transparent (dark: `rgba(255,255,255,0.02)`)
- **Border:** 1px `var(--color-graphite)`
- **Hover:** Background `var(--color-surface-tint)`, border `var(--color-smoke)`
- **Text:** `var(--color-mist)`
- **Border-radius:** 6px
- **Padding:** 8px 14px, Inter 13px / weight 400

### 3. Course Card (LMS Core)
**Role:** Primary card surface for catalog exploration and enrolled student dashboard
- **Container:** Background `var(--color-carbon)`, border 1px `var(--color-graphite)`, radius 12px, padding 16px
- **Thumbnail:** 16:9 aspect ratio, radius 8px, background `var(--color-obsidian)`, subtle inner hairline border
- **Title:** Inter 16px / weight 510, color `var(--color-paper)`
- **Instructor / Meta:** Inter 13px / weight 400, color `var(--color-ash)`
- **Progress Slot:** Embedded Bitmask Progress Bar along the card bottom (or badge showing "12/20 Lessons")

### 4. Bitmask Progress Bar (LMS Core)
**Role:** High-efficiency course completion indicator driven by database `VARBIT` completion bitmask
- **Track:** Height 4px (or 6px for active view), background `var(--color-graphite)`, border-radius 9999px
- **Fill:** Background `var(--color-precision-blue)` (or `var(--color-success)` when 100% complete)
- **Counter:** Berkeley Mono 12px, color `var(--color-fog)`, e.g., `18/24 (75%)`
- **Segmented Variant:** For individual module steps, renders N distinct pill segments (active: blue, done: green, uncompleted: graphite)

### 5. Physical Library Code Voucher Redeem Card (LMS Core)
**Role:** Syria/MENA offline economy redemption modal/card for unlocking courses via physical scratch-off cards
- **Container:** Card `var(--color-carbon)`, 12px radius, hairline border, padding 24px
- **Header:** "Redeem Academy Access Code" with a subtle lock-to-unlock key icon
- **PIN Input Field:** Monospace uppercase segmented text input (`input-otp` layout), font Berkeley Mono 18px, tracking 0.1em, background `var(--color-obsidian)`, border 1px `var(--color-smoke)`
- **Validation State:**
  - *Valid:* Border brightens to `var(--color-success)`, shows course title and instant unlock checkmark
  - *Invalid / Expired:* Border shifts to `var(--color-destructive)`, helper text in Ash: "Code already used or invalid"
- **CTA:** Full-width Primary Action Button: "Activate Course Access"

### 6. Video Stream Player HUD (LMS Core)
**Role:** Minimalist, low-bandwidth video player container for serverless HLS streaming
- **Container:** 16:9 ratio, full-bleed or framed in 12px card, canvas `var(--color-void)`
- **Overlay HUD:** Autohides on play; background subtle dark gradient (`rgba(9,9,11,0.85)` to transparent)
- **Controls:** Play/Pause, scrubber with Precision Blue buffered progress line, lesson chapter markers
- **Bandwidth Selector:** Pill dropdown allowing quick switches: `240p`, `360p`, `720p`, `Auto (HLS)` — optimized for low-bandwidth connections

### 7. Badge & Tag System
**Role:** Course categories, difficulty levels, lesson status
- **Default:** Background `var(--color-surface-tint)`, text `var(--color-mist)`, radius 4px, padding 2px 8px, Inter 12px
- **Status Variants:**
  - *Completed:* Green tint `rgba(34,197,94,0.12)`, text `var(--color-success)`
  - *In Progress:* Blue tint `rgba(37,99,235,0.12)`, text `var(--color-precision-blue)`
  - *Quiz Required:* Amber tint `rgba(245,158,11,0.12)`, text `var(--color-warning)`

---

## Do's and Don'ts

### Do
- Use **Precision Blue (`#2563eb`)** exclusively for the primary interactive action per section.
- Keep background surfaces strictly on the **neutral Zinc spectrum** (`#09090b` dark / `#ffffff` light) to prevent color pollution during long study sessions.
- Use **Berkeley Mono** for voucher PIN codes, lesson progress counters (`12/24`), timestamps, and code exercises.
- Ensure all interactive elements have visible focus rings (`rgba(37, 99, 235, 0.5)`) for full keyboard accessibility.
- Design every empty state with context, helpful copy, and a primary blue CTA ("Browse Catalog", "Enter Voucher Code").
- Keep hairline borders at 0.5px–1px (`--color-graphite`) to maintain precision elevation without muddy shadow stacks.

### Don't
- Do not use neon acid-lime or harsh yellow for primary actions — use Precision Blue.
- Do not apply saturated chromatic colors to card or page backgrounds; all canvas foundations must remain pure neutral Zinc.
- Do not hide empty states behind raw strings like "No courses found"; empty states must guide the learner.
- Do not omit the bitrate selector on video player frames — low-bandwidth Syrian/MENA users require manual 240p/360p controls.
- Do not use decorative gradients on buttons or content cards; keep surfaces flat, crisp, and quiet.
- Do not use fonts below 13px for instructional body copy.

---

## Surfaces & Elevation

### Dark Mode (Command Center)
- **Level 0 (Void `#09090b`):** Page background, full-bleed canvas
- **Level 1 (Carbon `#121215`):** Course cards, sidebar navigation, top header
- **Level 2 (Obsidian `#18181b`):** Modal dialogs, dropdown menus, voucher input backgrounds
- **Level 3 (Surface-Tint `#222226`):** Active lesson rows, hover states, ghost button fills
- **Border Definition:** 0.5px–1px hairline border (`#27272a` Graphite) separates surfaces without relying on ambient shadows.

### Light Mode (High-Clarity Study Mode)
- **Level 0 (Void `#ffffff`):** Page background, reading canvas
- **Level 1 (Carbon `#f9fafb`):** Cards, lesson navigation panels, lesson content blocks
- **Level 2 (Obsidian `#f4f4f5`):** Dialog sheets, dropdown menus, search command palette
- **Level 3 (Surface-Tint `#ececee`):** Hover states, subtle table header fills
- **Border Definition:** 1px hairline border (`#e4e4e7` Graphite) defines clean boundaries.

---

## Quick Start: Codebase Synchronization

### CSS Custom Properties (`globals.css`)

```css
:root {
  /* Foundations — Light Mode */
  --background: oklch(1 0 0);          /* #ffffff */
  --foreground: oklch(0.145 0 0);      /* #09090b */
  --card: oklch(0.985 0 0);            /* #f9fafb */
  --card-foreground: oklch(0.145 0 0);
  --popover: oklch(0.96 0 0);          /* #f4f4f5 */
  --popover-foreground: oklch(0.145 0 0);
  --muted: oklch(0.96 0 0);
  --muted-foreground: oklch(0.55 0 0); /* #71717a */
  --border: oklch(0.92 0 0);           /* #e4e4e7 */
  --input: oklch(0.86 0 0);            /* #d4d4d8 */

  /* Interactive Primary — Precision Blue */
  --primary: oklch(0.55 0.22 260);     /* #2563eb */
  --primary-foreground: oklch(1 0 0);  /* #ffffff */
  --ring: oklch(0.55 0.22 260 / 0.5);

  /* Status Semantics */
  --success: oklch(0.62 0.19 145);     /* #16a34a */
  --destructive: oklch(0.57 0.22 27);  /* #dc2626 */
  --warning: oklch(0.68 0.18 75);      /* #d97706 */

  --radius: 0.375rem; /* 6px */
}

.dark {
  /* Foundations — Dark Mode */
  --background: oklch(0.145 0 0);      /* #09090b */
  --foreground: oklch(0.985 0 0);      /* #ffffff */
  --card: oklch(0.18 0 0);             /* #121215 */
  --card-foreground: oklch(0.985 0 0);
  --popover: oklch(0.21 0 0);          /* #18181b */
  --popover-foreground: oklch(0.985 0 0);
  --muted: oklch(0.21 0 0);
  --muted-foreground: oklch(0.55 0 0); /* #71717a */
  --border: oklch(0.27 0 0);           /* #27272a */
  --input: oklch(0.35 0 0);            /* #3f3f46 */

  /* Interactive Primary — Precision Blue */
  --primary: oklch(0.55 0.22 260);     /* #2563eb */
  --primary-foreground: oklch(1 0 0);  /* #ffffff */
  --ring: oklch(0.55 0.22 260 / 0.5);

  /* Status Semantics */
  --success: oklch(0.72 0.19 145);     /* #22c55e */
  --destructive: oklch(0.63 0.22 27);  /* #ef4444 */
  --warning: oklch(0.75 0.18 75);      /* #f59e0b */
}
```

### Tailwind v4 Theme Tokens

```css
@theme {
  /* Colors — Neutrals */
  --color-void: #09090b;
  --color-carbon: #121215;
  --color-obsidian: #18181b;
  --color-surface-tint: #222226;
  --color-graphite: #27272a;
  --color-smoke: #3f3f46;
  --color-ash: #71717a;
  --color-fog: #a1a1aa;
  --color-mist: #e4e4e7;
  --color-paper: #ffffff;

  /* Colors — Interactive Precision Blue */
  --color-precision-blue: #2563eb;
  --color-blue-hover: #1d4ed8;
  --color-blue-active: #1e40af;
  --color-blue-subtle: #eff6ff;

  /* Colors — Educational Status */
  --color-success: #16a34a;
  --color-destructive: #dc2626;
  --color-warning: #d97706;
  --color-signal-teal: #0284c7;
  --color-iris-violet: #6366f1;

  /* Typography */
  --font-inter-variable: 'Inter Variable', ui-sans-serif, system-ui, sans-serif;
  --font-berkeley-mono: 'Berkeley Mono', ui-monospace, monospace;

  /* Border Radii */
  --radius-sm: 2px;
  --radius-md: 6px;
  --radius-xl: 12px;
  --radius-full: 9999px;
}
```
