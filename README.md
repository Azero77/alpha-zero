# 🎓 AlphaZero Learning Academy

> **Empowering Students. Elevating Teachers. Revolutionizing Digital Education.**
> **Shopify For Schools"

As a school or academy they can manage:
their own academy
their own courses
their own branding
their own teachers

AlphaZero is a next-generation **Tenant-Based SaaS e-learning platform** designed for schools and universities to launch their own online academies. Built for **low-bandwidth reliability**, **localized payment challenges** (offline codes), and **professional pedagogical workflows** (handwritten exam grading).

---

## 🚀 MVP (Minimum Viable Product)

### 🎯 Core Goals
The MVP focuses on a robust, scalable infrastructure for a multi-tenant environment:
- 🏛️ **Multi-Tenant Architecture:** Every school/university has its own isolated environment, branding, and staff.
- 🏗️ **Professional Content Lifecycle:** A structured workflow from Worker (Draft) -> Teacher (Validation) -> Course Manager (QA/Approval).
- 🧮 **Hybrid Assessment:** Automated MCQ/Math quizzes + manual grading for handwritten exam uploads.
- 🎟️ **Library Economy:** A secure, offline payment system using redeemable access codes sold through physical libraries.
- 💬 **Course Communities:** Threaded discussions integrated directly into lessons to facilitate peer-to-peer and teacher-led learning.

---

## 🧱 Technical Stack

| Layer | Technology |
|-------|------------|
| **Frontend** | React (Web) & Flutter (Mobile) |
| **Backend** | ASP.NET Core Web API with JWT Authentication |
| **Database** | PostgreSQL (Relational) & DynamoDB (Quizzes/Scalable Data) |
| **Storage** | AWS S3 with CloudFront/EC2 Caching for low-latency video |
| **Infrastructure**| AWS (SaaS Optimized) |
| **Payments** | Offline Library Codes + Local Gateways (Syriatel Cash, Sham Cash, etc.) |

---

## 🧩 MVP Feature Breakdown

| Feature | Description |
|----------|--------------|
| **1. Multi-Tenant Engine** | Instant tenant provisioning with custom themes, subjects, and grade mapping. |
| **2. Content Pipeline** | Role-based submission and approval workflow for videos and materials. |
| **3. Hybrid Quiz System** | LaTeX-supported math expressions, automated scoring, and photo-based manual grading. |
| **4. Library Code System** | Batch code generation, inventory tracking, and secure redemption for offline sales. |
| **5. Community Q&A** | Lesson-specific discussion threads with "Verified Answer" status by teachers. |
| **6. Advanced Analytics** | Course "drop-off" heatmaps, student rank leaderboards, and "At-Risk" student detection. |
| **7. Notification Engine** | Real-time alerts for grading, content approvals, and community interactions. |

---

## 🧑‍💼 Roles & Permissions (RBAC)

| Role | Responsibility |
|------|---------------|
| **Tenants Manager** | Global platform owner; manages all tenants and branding defaults. |
| **Tenant Admin** | Manages a specific school's statistics, staff, and financial reports. |
| **Tenant Course Manager**| QA expert; reviews and approves content before it goes live. |
| **Teacher** | Subject matter expert; writes exams and grades handwritten submissions. |
| **Course Worker** | Production "doer"; uploads videos and drafts quiz questions. |
| **Library Manager** | Mints and tracks access codes for physical distribution. |
| **Library Accountant** | Audits code generation and maps courses to the library system. |
| **Library Worker** | Front-end retail role for retrieving and generating sales codes. |
| **Tenant Support** | Moderates school community and manages user/subject provisioning. |
| **App Support** | Manages global/public discussions and non-tenant issues. |
| **Student** | The learner; consumes content, takes exams, and joins the community. |

---

## 🔔 Audit & Security
- **Immutable Logs:** Every enrollment deactivation and code generation is logged for fraud prevention.
- **Tenant Isolation:** Rigorous data separation ensuring students only see content from their specific institution.
