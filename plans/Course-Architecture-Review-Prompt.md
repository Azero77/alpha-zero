# 🪐 Architectural RFC & Peer Review Prompt: Unified Curriculum Engine & Cross-Module Asset Authorization

> **Instructions for the Reviewing AI:**
> You are acting as a Principal Systems Architect and Staff Distributed Systems Engineer. Review the following architecture proposal, architectural dilemmas, and trade-offs for an enterprise multi-tenant e-learning platform.
> Analyze the chosen design, challenge any potential blind spots, evaluate the pros and cons, and provide concrete feedback on scalability, security, domain modeling, and user experience.

---

## 1. System Background & Core Technical Constraints

- **Platform:** **AlphaZero** is a high-performance, multi-tenant SaaS e-learning platform built for low-bandwidth environments (Syria/MENA).
- **Architecture:** **Modular Monolith** adhering strictly to **Enterprise Clean Architecture**:
  - **Isolation Mandate:** Modules (`Identity`, `Courses`, `Assessments`, `VideoUploading`, `Library`, `Tenants`) must remain strictly isolated. No cross-module database joins or foreign keys.
  - **Inter-Module Communication:** Asynchronous events via in-memory MassTransit Mediator or external SQS; synchronous point-to-point queries via internal MediatR request/response contracts (`IModuleBus`).
  - **Tenant Awareness:** Strict multi-tenant isolation. Every request is scoped to a `TenantId` via `ITenantProvider`. Cross-tenant data access is forbidden (enforced via global EF Core query filters and BOLA checks).
- **Progress Tracking:** Completion logic **MUST** use a bitmask (`VARBIT` in PostgreSQL) on the `Enrollment` aggregate. Each curriculum item holds an immutable, monotonic `BitIndex` allocated at drafting time. The total course items equal `NextAvailableBitIndex`.
- **Security & Authorization (IAM):** An AWS-style IAM engine evaluating:
  - **Resource ARNs:** Formatted as `az:{service}:{tenantId}:{resourcePath}` (e.g. `az:video:tenant-1:video/uuid`, `az:course:tenant-1:course/uuid`).
  - **Resource Patterns:** Permission scopes with wildcards (e.g. `az:*:{tenantId}:course/{courseId}/*`).

---

## 2. The Problem Statement & Architectural Drivers

### 2.1 Domain vs. API Layer Drift
- **The Domain & Database:** Recently refactored to support a rich, flexible curriculum hierarchy:
  - `Course` (Aggregate Root) &rarr; `CourseSection` &rarr; `CurriculumItem` &rarr; `CurriculumResource` (Owned Entity).
  - An item has a polymorphic `MainType` (`"Video"`, `"Quiz"`, `"Document"`, `"Inline"`, etc.) and contains an ordered collection of resources classified as `Primary` (the main lecture/quiz) or `Auxiliary` (PDF slides, worksheets, practice quiz, formula sheet).
  - Persistence is mapped via PostgreSQL and EF Core (`CurriculumItems` table + owned `CurriculumResources` table storing `Arn`, `Type`, `Order`, and JSONB `Metadata`).
- **The API Layer (The Legacy Debt):**
  - Still exposes only two binary endpoints from an older version:
    - `POST /courses/{courseId}/sections/{sectionId}/lessons` (strictly forces a single `VideoId`).
    - `POST /courses/{courseId}/sections/{sectionId}/assessments` (strictly forces creating a new assessment).
  - It is impossible to attach auxiliary resources (e.g. PDF notes to a video), create document/inline items, reorder resources, or link existing assessments.

### 2.2 Teacher Authoring UX Problem (Tab Juggling)
- Currently, teachers must juggle 3–4 separate browser tabs (Video Uploader tab, Assessment Studio tab, Course Editor tab), manually copy/paste UUIDs, and wait for asynchronous transcoding jobs before they can link content into a course.
- **Requirement:** A single-canvas, in-situ authoring studio inside the Course Architect where videos, quizzes, PDF documents, and inline text can be uploaded/created directly without leaving the page.

### 2.3 The Core IAM Dilemma: Asset Reusability vs. Scoped Student Authorization
- Videos, assessments, and documents are created as **tenant-wide digital assets** with their own canonical ARNs:
  - `az:video:{tenant}:video/{videoId}`
  - `az:assessment:{tenant}:assessment/{assessmentId}`
  - `az:document:{tenant}:document/{documentId}`
- A teacher wants to reuse the exact same video or quiz asset across:
  - Course A, Section 1, Item 1
  - Course B, Section 3, Item 2
  - A tenant-wide Library offline download package
  - A future public academy blog post
- **The Dilemma:** How can students be authorized to watch the video or submit the quiz when accessing it through Course A, while preventing unauthorized access if they didn't enroll in Course B, **without** dynamically attaching and synchronizing dozens of individual video IAM policies to the student's principal record upon enrollment?

---

## 3. The 5 Architectural Dilemmas & Proposed Decisions

---

### Decision 1: Course Authoring & In-Situ Coordination
**How should the single-canvas Course Architect orchestrate uploads and creations across specialized modules without tab switching?**

#### Proposed Approach:
**In-Situ Studio with Direct-to-S3 Pre-signed Uploads & Embedded Drawers.**
The frontend Course Architect coordinates directly with each module's specialized endpoints via slide-over drawers and modals:
- Videos stream directly from the browser to S3 via pre-signed URLs obtained from `POST /api/video-uploading/upload`.
- Quizzes are created or selected via a slide-over drawer calling `POST /assessments`.
- Documents stream directly to S3 via a new `Documents` module.
- The resulting ARNs are assembled in the browser and sent in a single atomic call to `POST /courses/{courseId}/sections/{sectionId}/items`.

#### Trade-off Analysis:

| Criteria | Option A: In-Situ Direct-to-S3 (Proposed) | Option B: Courses Module Gateway Facade (BFF) | Option C: Embedded Micro-Frontends (iFrames) |
| :--- | :--- | :--- | :--- |
| **Server Bandwidth & Memory** | **Near Zero:** Multi-GB files stream directly to AWS S3. | **Severe Bottleneck:** Multi-GB files pass through the API server memory buffers. | **Near Zero:** Streams to S3 via sub-app. |
| **Module Clean Architecture** | **Strictly Preserved:** Courses module only deals with ARNs, never raw binaries. | **Violated:** Courses module becomes coupled to S3 multipart uploads, video transcoding, etc. | **Preserved:** Separate sub-applications. |
| **User Experience (UX)** | **Smooth & Optimistic:** Progress bars in-situ, teacher continues drafting while S3 streams. | **Laggy:** Request blocks until server buffers and re-transmits payload. | **Janky:** Cross-frame styling mismatches, scroll jumps, and fragile auth handshakes. |
| **Frontend Complexity** | Moderate (frontend needs drawer components for each asset type). | Low (single endpoint for the frontend). | High (managing `postMessage` state & cross-origin iframe security). |

---

### Decision 2: Asset Reusability & ARN Permission Tracking
**How do we authorize student access to reusable assets (videos, quizzes, PDFs) across different courses, sections, and blogs without IAM policy explosion?**

#### Proposed Approach:
**Container-Derived Scoped Access (Contextual Gateway Authorization).**
- Reusable media assets maintain their canonical ARNs (`az:video:{tenant}:video/{videoId}`).
- Students are **never** granted direct IAM policies on raw video or document ARNs.
- Instead, the student's IAM assignment is strictly scoped to the container course:
  `Role: Student` on scope `az:course:{tenant}:course/{courseId}/*`.
- When a student plays a video or downloads a document, the request is dispatched through the course context:
  `GET /courses/{courseId}/items/{itemId}/resources/{resourceArn}/access` (or `GET /api/video/{videoId}?courseId={courseId}`).
- The gateway checks:
  1. Does caller have `courses:View` on `az:course:{tenant}:course/{courseId}`?
  2. Is the caller actively enrolled in the course?
  3. Does `itemId` in `courseId` actually contain this `resourceArn`?
- Once verified, the gateway generates a short-lived, signed CloudFront cookie/URL or HLS stream manifest for the student.

#### Trade-off Analysis:

| Criteria | Option A: Container-Derived Scoped Access (Proposed) | Option B: Direct Per-Asset Policy Delegation | Option C: Pre-signed Token Capability Grants |
| :--- | :--- | :--- | :--- |
| **Database Scalability** | **O(1) Policy Assignment:** One policy per enrollment (`az:course:.../*`), regardless of whether the course has 10 or 200 videos. | **O(N) Policy Explosion:** Enrolling in a 60-item course requires writing 60+ individual IAM policy assignments in the DB. | **O(1) Database Footprint:** Tokens are stateless and signed by private key. |
| **Asset Reusability** | **100% Reusable:** The same video ARN can be used in Course 1, Course 2, or a public blog. Access depends entirely on container enrollment. | **Security Vulnerability:** If Video 1 is in Course A and Course B, granting direct permission on Video 1 leaks access across courses. | **100% Reusable:** Tokens bind asset ARN to user session. |
| **Revocation & Expiration** | **Instantaneous:** Revoking or expiring a course enrollment immediately blocks access to all 60 videos in that course. | **High Risk:** Must batch-delete 60 policy assignments. Partial failures leave orphan permissions. | **Delayed:** Must wait for short-lived token to expire or manage token revocation lists. |
| **Implementation Complexity** | Low-to-Moderate: Gateway verifies course placement before calling streaming service. | High: Complex event-driven synchronization between enrollment and IAM policy tables. | High: Requires key management, clock-skew mitigation, and token distribution service. |

---

### Decision 3: Document File Uploads & Inline Rich Text Architecture
**How should supplementary PDF documents and inline reading text between resources be architected and persisted?**

#### Proposed Approach:
1. **Dedicated Documents Module (`AlphaZero.Modules.Documents`):**
   - Follows the lightweight Clean Architecture pattern of `VideoUploading`.
   - Endpoints: `POST /api/documents/upload` (returns S3 pre-signed `PUT` URL) and `GET /api/documents/{id}` (returns download URL).
   - Generates canonical ARNs: `az:document:{tenantId}:document/{documentId}`.
2. **Embedded JSONB for Inline Text:**
   - Inline rich text, reading notes, and markdown summaries are stored directly in `CurriculumResource.Metadata` (PostgreSQL `JSONB`) under ARN `az:inline:{tenantId}:inline/{uuid}`.
   - Requires zero external S3 buckets, zero S3 latency, and zero additional relational tables. It loads instantly in the `GetCourse` payload.

#### Trade-off Analysis:

| Criteria | Option A: Dedicated Doc Module + JSONB Inline (Proposed) | Option B: Unified "Assets" Module for All Files | Option C: Store Everything in Courses Module |
| :--- | :--- | :--- | :--- |
| **Clean Module Boundaries** | **High:** Documents can be reused in future modules (Library, Blog, Student Submissions); Courses remains focused on curriculum. | **Moderate:** High initial refactoring cost to merge `VideoUploading` and document storage. | **Low:** Violates Clean Architecture by turning Courses into an asset blob storage engine. |
| **Inline Text Performance** | **Optimal (<5ms):** Markdown loads directly in the course aggregate query from PostgreSQL JSONB. | **Slower:** Joins on generic asset table or S3 fetch for short text snippets. | **Fast:** Stored in course DB, but clutters course schema. |
| **Refactoring Blast Radius** | **Isolated:** Leaves existing `VideoUploading` module intact while adding a small, dedicated `Documents` module. | **High:** Requires rewriting `VideoUploading`, migrations, and AWS SQS consumers. | **Moderate:** Bloats Courses module with S3 AWS SDK dependencies. |

---

### Decision 4: Courses Module API Contracts & Breaking Change Strategy
**How should the legacy `/lessons` and `/assessments` endpoints be transitioned to the new multi-resource curriculum design?**

#### Proposed Approach:
**Breaking Clean Replacement.**
- Permanently delete the legacy binary endpoints:
  - `DELETE POST /courses/{CourseId}/sections/{SectionId}/lessons`
  - `DELETE POST /courses/{CourseId}/sections/{SectionId}/assessments`
- Introduce unified RESTful endpoints:
  - `POST /courses/{CourseId}/sections/{SectionId}/items` (Creates an item with primary and optional auxiliary resources).
  - `POST /courses/{CourseId}/items/{ItemId}/resources` (Appends an auxiliary resource).
  - `DELETE /courses/{CourseId}/items/{ItemId}/resources/{ResourceArn}` (Detaches an auxiliary resource).
  - `PUT /courses/{CourseId}/items/{ItemId}/resources/reorder` (Reorders resources within an item).
  - `DELETE /courses/{CourseId}/items/{ItemId}` (Soft-deletes item).
  - `DELETE /courses/{CourseId}/sections/{SectionId}` (Soft-deletes section).
- Refactor all existing integration tests in `tests/Modules/Courses` directly to the new contracts.

#### Trade-off Analysis:

| Criteria | Option A: Breaking Clean Replacement (Proposed) | Option B: Backward-Compatible Facade Wrapper | Option C: Additive Endpoints Only |
| :--- | :--- | :--- | :--- |
| **Codebase Cleanliness** | **Zero Debt:** Single source of truth for item creation; no redundant legacy endpoints or handlers. | **Moderate Debt:** Must maintain duplicate endpoints and translation logic. | **High Debt:** Legacy binary model remains the primary path, leaving curriculum design half-adopted. |
| **Test Suite Alignment** | **Complete:** All integration tests directly assert the modern multi-resource domain model. | **Masked:** Tests pass against legacy wrappers, hiding potential gaps in the modern API. | **Poor:** Multi-resource capabilities remain largely untested. |
| **Migration Effort** | Requires updating ~6 integration test files and frontend mock clients. | Lower upfront effort, but defers inevitable cleanup. | Lowest immediate effort, highest permanent maintenance cost. |

---

### Decision 5: Progress Tracking (`VARBIT`) Invariants with Multi-Resource Items
**When an item contains multiple resources (e.g., Primary Video + Auxiliary Practice Quiz + PDF slides), what triggers the completion of the item's `BitIndex` in the progress bitmask?**

#### Proposed Rule:
- **Primary-Driven Completion:**
  - By default, completion of the **Primary Resource** (e.g. video watched via completion event, or primary exam passed via `AssessmentGradingCompletedConsumer`) flips the item's `BitIndex` in PostgreSQL `VARBIT`.
  - Auxiliary resources (lecture slides, formula sheets) are non-blocking supplementary materials unless an item is explicitly configured with `RequireAllResourcesCompleted = true`.
- **Why?** Progress tracking represents pedagogical advancement through the syllabus. Auxiliary materials are supplementary aids; gating completion on downloading a PDF formula sheet harms the student UX, particularly in low-bandwidth offline environments.

---

## 4. Questions for the Peer Reviewer

1. **On the IAM Scoped Authorization Pattern:** Does the **Container-Derived Scoped Access** model present any security edge cases when scaling to thousands of concurrent video streams or CDN caching layers? How would you structure the CloudFront signed cookie generation to keep latency under 50ms?
2. **On the In-Situ Authoring Flow:** Given that teachers may upload 1GB+ video files while drafting courses, how would you handle draft state persistence if the teacher closes their laptop mid-upload? Should the `CurriculumItem` be saved in an `UnpublishedDraft` state immediately when the pre-signed URL is issued?
3. **On Document Module Scope:** Is creating a standalone `AlphaZero.Modules.Documents` module optimal, or should it be generalized as an `Assets` module that also manages generic attachments (e.g., audio, images, archives) across all academy modules?
4. **On Bitmask Progress Tracking:** Are there any edge cases with assigning `BitIndex` monotonically at item creation if an item is later soft-deleted or reordered within sections?
5. **Overall Assessment:** What is the single biggest risk in this architectural plan, and how would you mitigate it?
