# 📄 Product Requirement Document (PRD): AlphaZero Learning Academy

## 1. Project Overview
**AlphaZero** is a high-performance, multi-tenant SaaS e-learning platform. It is designed to provide schools and universities with a white-labeled "Online Academy" that functions reliably in low-bandwidth environments and solves localized payment challenges through a physical library code economy.

### Vision
To empower educational institutions by bridging the gap between traditional teaching and digital scalability, focusing on rigorous academic workflows (manual grading) and community-led support.

---

## 2. Target Audience & Stakeholders
*   **Primary Users:** High school and University students.
*   **Secondary Users:** Teachers and Content Producers (Workers).
*   **Clients:** Schools/Universities (Tenants).
*   **Retail Partners:** Physical Libraries (Code Distributors).

---

## 3. Functional Pillars

### 🏛️ Multi-Tenancy & Branding
*   **Isolation:** Each tenant (e.g., `oxford.alphazero.com`) must have isolated data, subjects, and staff.
*   **Customization:** Tenants manager can configure themes (colors, logos) per tenant.
*   **Subdomain Support:** Instant provisioning of subdomains for new institutions.

### 🏗️ Professional Content Lifecycle (QA Workflow)
*   **Role-Based Production:** 
    *   **Course Workers** upload videos and draft quizzes.
    *   **Teachers** validate the academic accuracy.
    *   **Tenant Course Managers** perform final QA and change status to `Approved`.
*   **Versioning:** Lessons remain in `Draft` until approved, invisible to students.

### 🧮 Hybrid Assessment System
*   **Automated:** Supports MCQ, True/False, and LaTeX-based Math expressions.
*   **Handwritten:** Students upload photos of handwritten work; Teachers use a specialized grading interface for manual correction and feedback.
*   **Exam Types:** Course-specific quizzes, Tenant-wide exams, and global "Public" exams.

### 🎟️ Offline-First Economy (Library Codes)
*   **Code Generation:** Library Managers mint unique access codes.
*   **Inventory Tracking:** Accountant role audits the attachment of courses to libraries and monitors sales logs.
*   **Redemption:** Simple, low-bandwidth UI for students to enter a code and instantly unlock courses.

### 💬 Community & Gamification
*   **Contextual Threads:** Discussions are lesson-specific to keep questions relevant.
*   **Verified Answers:** Teachers can mark peer answers as "Official."
*   **Motivation:** Tenant-specific leaderboards and badges based on achievement points.

---

## 4. User Roles (RBAC)
| Role | Primary Responsibility |
| :--- | :--- |
| **Tenants Manager** | Global admin; creates tenants and manages platform-wide health. |
| **Tenant Admin** | Manages school-specific staff, subjects, and financial reports. |
| **Tenant Course Manager** | Final QA; approves content and assists teachers technically. |
| **Teacher** | Subject Expert; writes exams and grades handwritten work. |
| **Course Worker** | Content producer; handles uploads and community moderation. |
| **Library Manager** | Manages the creation and distribution of course access codes. |
| **Library Accountant** | Audits library financials and maps courses to retail points. |
| **Library Worker** | Retail point-of-sale; retrieves/generates codes for customers. |
| **Tenant Support** | Handles local school tickets and moderates comments. |
| **App Support** | Manages global platform discussions. |
| **Student** | Learner; consumes content and takes assessments. |

---

## 5. Technical Requirements & Constraints

### 🧠 Performance & Storage
*   **Progress Tracking:** MUST use **Bitmasking** (`VARBIT`) for lesson completion to minimize database size and API payload.
*   **Adaptive Streaming:** HLS/Dash streaming via AWS S3/CloudFront to handle slow Syrian internet speeds.
*   **Caching:** Offline video caching support in the mobile (Flutter) app.

### 🛡️ Security & Integrity
*   **Session Control:** One active session per student. Logging in on a new device MUST terminate the previous session immediately.
*   **Data Separation:** Row-level security or strict filtering by `TenantId` across all tables.
*   **Audit Logs:** Immutable logs for all code redemptions and enrollment deactivations.

---

## 6. MVP Success Metrics
1.  **Tenant Onboarding:** New tenant setup completed in < 5 minutes.
2.  **Code Redemption:** Success rate of offline code entries > 99%.
3.  **Engagement:** > 30% of students participating in lesson-specific Q&A.
4.  **Grading Speed:** Reduction in teacher grading time through the handwritten-interface UI.
