# Alpha Zero: Ultimate UI/UX Design System & Global Page Architecture

This document is the master design specification for **Alpha Zero**, a multi-tenant, Arabic/English e-learning ERP. It serves as a comprehensive prompt for high-fidelity UI/UX generation, focusing on extreme scalability and bidirectional (LTR/RTL) excellence.

---

## 1. Visual Identity & "Arabic-First" Design System
The system must support **Bidirectional (BiDi)** layouts natively. The UI should not just be "mirrored" but "culturally and linguistically optimized."

### Color Palette (Modern Oceanic Professional)
| Element | Hex Code | Usage |
| :--- | :--- | :--- |
| **Primary Brand** | `#0A4174` | Main Headers, Active States, Primary Buttons. |
| **Secondary/Accent**| `#7BBDE8` | High-priority CTAs, Progress Bars, Achievement highlights. |
| **Background** | `#BDD8E9` | Light Mode Background, Paper-feel containers. |
| **Surface/Neutral** | `#6EA2B3` | Secondary Buttons, Cards, Muted UI elements. |
| **Deep Neutral** | `#001D39` | Primary Text (High contrast), Dark Mode Background. |
| **Subtle Border** | `#49769F` | Dividers, Input Borders, Outlines. |
| **Success/Teal** | `#4E8EA2` | Validated states, "Official" markers, Success alerts. |

### Bidirectional Typography (BiDi)
- **Latin Font:** `IBM Plex Sans` (Modern, Technical).
- **Arabic Font:** `IBM Plex Sans Arabic` (Kufi-inspired, high legibility).
- **Vertical Rhythm:** Arabic glyphs require **15-20% more line-height** than Latin. The grid must adjust dynamically to prevent "crowding" in RTL mode.
- **Numbers:** Use "Western Arabic" numerals (1, 2, 3) for technical data tables but ensure font support for "Eastern Arabic" (١, ٢, ٣) if requested by specific tenants.

---

## 4. Cross-Platform & Adaptive Strategy
The system must adapt seamlessly between a high-density **Desktop Web ERP** and a touch-optimized **Mobile Application (Flutter)**.

### Platform-Specific Navigation
| Platform | Primary Navigation | Interaction Pattern |
| :--- | :--- | :--- |
| **Desktop Web** | **Sticky Sidebar** (Expandable/Collapsible) | Mouse-hover tooltips, Right-click context menus. |
| **Tablet/iPad** | **Persistent Sidebar** or **Hybrid Rail** | Touch-friendly targets (min 44px), Swipe-to-dismiss. |
| **Mobile App** | **Bottom Tab Bar** (5 key icons) | Thumb-zone optimization, Pull-to-refresh, Native share. |

### Mobile-First Native Features
- **Offline Mode UI:** Clear visual indicators for "Downloaded" content vs. "Cloud" content. Include a "Downloads Manager" page in the mobile-specific sitemap.
- **Push Notification Center:** A dedicated mobile view for real-time alerts (Exam graded, New lesson, Admin announcement).
- **Haptic Feedback:** Define "Success" (short vibration) and "Error" (double pulse) haptics for critical actions like Code Redemption.

---

## 5. Comprehensive Page Architecture (The "Hundred Pages" Sitemap)

### A. Student Experience (Learning Domain)
1. **Global Dashboard:** Course progress, streak tracking, recent lesson quick-resume.
2. **Course Catalog:** Filter by Grade, Subject, Teacher, or Price.
3. **Course Landing Page:** Syllabus, Teacher Bio, Reviews, "Redeem Access" CTA.
4. **Adaptive Course Player:** Video, Note-taking widget, Resource downloads.
5. **Lesson-Specific Q&A:** Threaded discussions with "Verified by Teacher" badges.
6. **Quiz Interface (MCQ/True-False):** Timer-based, progress indicator, LaTeX math support.
7. **Handwritten Exam Portal:** Photo upload interface with camera integration.
8. **Graded Exam Review:** View teacher's red-pen annotations on uploaded photos.
9. **My Gradebook:** Cumulative scores across all enrolled subjects.
10. **Library Code Redemption Center:** Batch redemption and history.
11. **Tenant Profile:** Edit student info, one-session security settings.
12. **Public Square:** Global platform-wide community (non-tenant).

### B. Teacher & Content Production (Academic Domain)
1. **Teacher Dashboard:** Active students, pending grading queue, course drop-off stats.
2. **Course Management:** View/Edit course structure (Modules > Lessons).
3. **Exam Creator:** Form builder for MCQs and Handwritten prompts.
4. **Grading Queue (Handwritten):** Split-view UI (Student Photo vs. Feedback Form).
5. **Grading Queue (Auto):** Review/Override automated quiz results.
6. **Student Progress Analytics:** Identification of "At-Risk" students.
7. **Content Validation (Teacher View):** Review/Approve materials drafted by Workers.
8. **Worker Assignment:** Assign specific lessons to Course Workers for production.

### C. Course Worker (Operations Domain)
1. **Bulk Upload Interface:** Drag-and-drop for video lessons and PDF notes.
2. **Community Moderation:** Delete spam, escalate tickets to Support.
3. **Enrollment Deactivation:** Manual override for payment violations.
4. **QA Dashboard:** Track status of content (Draft -> Pending Review -> Approved).

### D. Library & Financials (Economy Domain)
1. **Library Dashboard:** Sales stats, low-inventory alerts for codes.
2. **Code Minting Engine:** Bulk generation of 100+ unique course codes.
3. **Inventory Management:** Track "Sold" vs. "Unsold" codes.
4. **Retail Point-of-Sale (POS):** Fast-search for student redemption or new sale.
5. **Accountant Audit Logs:** Immutable record of every code generated/mapped.
6. **Course Mapping:** Link physical libraries to specific digital content permissions.

### E. Tenant Administration (Management Domain)
1. **Institution Overview:** Total revenue, enrollment growth, staff performance.
2. **Staff Management:** Create/Manage Teacher, Worker, and Support accounts.
3. **Subject & Grade Mapping:** Define the school's academic hierarchy (e.g., 9th Grade Physics).
4. **White-Labeling Studio:** Dynamic theme editor (Logo, Primary Color, Custom Domain).
5. **Student Directory:** Search/Manage all enrolled students.
6. **Support Ticket System:** Internal school-level technical issues.

### F. Global Admin / Tenants Manager (Platform Domain)
1. **God-View Dashboard:** Global health, server load, storage usage across all tenants.
2. **Tenant Provisioning:** One-click setup for new schools (Subdomain, DB Isolation).
3. **Global Audit Logs:** Security monitoring for platform-wide threats.
4. **Billing & Subscriptions:** Manage tenant-level payments to Alpha Zero.

### G. Public Facing (Growth & Brand Domain)
1. **Global Landing Page:**
    - **Hero Section:** High-impact "Academic Excellence" messaging with Bilingual CTAs (Login/Join).
    - **Value Props:** Syrian-market specific (Offline codes, Low-bandwidth, Handwritten grading).
    - **Live Statistics:** Total students, Active tenants, Courses available.
    - **Course Highlights:** Carousel of top-rated subjects across the platform.
    - **Tenant Showcase:** Logos of verified schools and universities.
2. **"About Alpha Zero":**
    - **Our Mission:** Bridging the digital divide for academic institutions in challenging environments.
    - **The Technology:** Brief visual explanation of adaptive streaming and Bitmasked progress tracking.
    - **Team/Values:** Focus on academic integrity and teacher-led production.
3. **Library Partner Portal (Public Info):** How libraries can join the network.
4. **Platform Pricing (SaaS for Schools):** Tiered subscription models for potential tenants.

---

## 4. UX Logic & Complexity Management
- **Progressive Disclosure:** Use **Slide-overs (Drawers)** for detail-heavy views (e.g., Student Details) to keep the main data table visible.
- **Empty States:** Every page must have a high-quality Arabic/English empty state (e.g., "No exams to grade yet").
- **Offline-First Indicators:** Visual cues when content is "Available Offline" (Mobile) or being served via low-bandwidth HLS (Web).
- **Navigation Breadcrumbs:** Must reverse direction in RTL (e.g., Home < Grade < Physics in LTR becomes Physics > Grade > Home in RTL).

---

## 5. Implementation Strategy for Stitch AI
1. Generate **Modular Components** first (Buttons, BiDi-Inputs, RTL-Table-Headers).
2. Design the **Main Layout Shell** (Responsive sidebar, top global search).
3. Execute the **Top 5 High-Impact Pages** in high fidelity (Student Dashboard, Course Player, Grading Queue, Code Minting, Tenant Branding Studio).
4. Ensure all **Modals and Popups** are centered and respect BiDi alignment rules.
