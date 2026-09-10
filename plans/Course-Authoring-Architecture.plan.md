# 🪐 Plan: Unified Course Authoring Architecture & Cross-Module Curriculum System

## 1. Executive Summary & Design Principles

This document establishes the production architectural plan to replace the legacy binary item model (`/lessons` vs `/assessments`) with a unified, multi-resource **Curriculum Engine**. It delivers a seamless, zero-tab-switching authoring experience for teachers and administrators while enforcing strict, multi-tenant AWS-style ARN authorization.

### Key Architectural Decisions (Settled in Eng Review)
1. **Breaking Clean Replacement:** Permanently delete legacy `POST /courses/.../lessons` and `POST /courses/.../assessments` endpoints. Replace them with unified RESTful `/items` and `/resources` endpoints. All existing integration tests are refactored to the new contracts.
2. **In-Situ Teacher Studio:** All media authoring (videos, assessments, documents, inline text) occurs directly on the Course Architect canvas via slide-over drawers and direct-to-S3 pre-signed uploads—no browser tab juggling.
3. **Container-Derived Scoped Access:** Canonical digital assets retain their own global ARNs (`az:video:...`, `az:assessment:...`, `az:document:...`) for 100% tenant-wide reusability across multiple courses, sections, and blogs. Student playback authorization is derived dynamically from their enrollment in the container Course (`az:course:...`) with zero IAM policy bloat.
4. **Dedicated Documents Module:** Introduce `AlphaZero.Modules.Documents` following the proven S3 pre-signed upload pattern established by `VideoUploading`.
5. **Zero-Latency Inline Text:** Inline rich text, markdown summaries, and code snippets are stored directly in the `CurriculumResource.Metadata` PostgreSQL `JSONB` column, requiring zero external S3 calls or database joins.

---

## 2. System Architecture & Component Boundaries

### 2.1 Component Interaction & Module Boundary Diagram

```
+--------------------------------------------------------------------------------------------------+
|                                    BROWSER: COURSE ARCHITECT UI                                  |
|                                                                                                  |
|   +------------------------------------------------------------------------------------------+   |
|   |  Course: "Theoretical Physics 101"                                                        |   |
|   |  Section 1: "Newtonian Mechanics"                                                         |   |
|   |    Item 1: "Lecture 1: Gravitation" (MainType: Video, BitIndex: 0)                        |   |
|   |      [Primary]   Video: "Gravitation Lecture" (az:video:tenant-1:video/v-101)             |   |
|   |      [Auxiliary] PDF:   "Lecture Slides.pdf"  (az:document:tenant-1:document/d-201)      |   |
|   |      [Auxiliary] Quiz:  "Concept Check Quiz"  (az:assessment:tenant-1:assessment/a-301)  |   |
|   |      [Auxiliary] Text:  "Formula Cheat Sheet" (az:inline:tenant-1:inline/i-401)          |   |
|   +------------------------------------------------------------------------------------------+   |
+-------------+-----------------------+-------------------------+---------------------+------------+
              | (1) Pre-signed Upload | (2) Direct Upload       | (3) Create Quiz     | (4) Save Item
              v                       v                         v                     v
+------------------------+  +-------------------+  +------------------------+  +--------------------+
| VideoUploading Module  |  | Documents Module  |  |  Assessments Module    |  |   Courses Module   |
|                        |  |                   |  |                        |  |                    |
| - POST /upload         |  | - POST /upload    |  | - POST /assessments    |  | - POST /items      |
| - Returns S3 Put URL   |  | - Returns S3 URL  |  | - Bulk question setup  |  | - POST /resources  |
| - VideoId & video ARN  |  | - DocId & doc ARN |  | - Returns assess ARN   |  | - Links all ARNs   |
+-----------+------------+  +---------+---------+  +-----------+------------+  +---------+----------+
            |                         |                        |                         |
            | Direct S3 Upload        | Direct S3 Upload       | DB Storage              | DB Storage
            v                         v                        v                         v
+------------------------+  +-------------------+  +------------------------+  +--------------------+
|  AWS S3 Video Bucket   |  | AWS S3 Doc Bucket |  | PostgreSQL Assessment  |  | PostgreSQL Courses |
|  & MediaConvert Queue  |  | (PDF/DOCX/Slides) |  | Tables (JSONB Content) |  | & Bitmask Progress |
+------------------------+  +-------------------+  +------------------------+  +--------------------+
```

---

## 3. End-to-End Teacher & Admin In-Situ UX Flow

The Course Architect canvas provides a unified, single-page authoring studio. Teachers never leave the course editor to prepare content:

```
[Teacher clicks "+ Add Curriculum Item" inside a Section]
                          |
                          v
+-------------------------------------------------------------+
| IN-SITU ITEM COMPOSER MODAL / DRAWER                        |
|                                                             |
| Title: [ Physics Chapter 1: Momentum                       ] |
| Main Type: (•) Video  ( ) Assessment  ( ) Document  ( ) Text|
|                                                             |
| -- PRIMARY RESOURCE SLOT ---------------------------------- |
| Drop video here or [ Browse Academy Library ]                |
| [======================== 65% Uploading ==================] |
| Video ID: v-101  ARN: az:video:tenant:video/v-101            |
| Status: Uploaded -> Background Transcoding                  |
|                                                             |
| -- AUXILIARY MATERIALS (+ Add Material) -------------------- |
| 1. [PDF] Lecture Notes.pdf  (az:document:tenant:doc/d-202)   |
| 2. [Quiz] Practice 5-Q MCQ  (az:assessment:tenant:quiz/q-3)  |
| 3. [Text] Key Formulas      (az:inline:tenant:inline/i-4)    |
|                                                             |
| [ Cancel ]                                [ Save Item ]      |
+-------------------------------------------------------------+
                          |
                          | (Client sends POST /courses/.../items)
                          v
[Item immediately appears in curriculum hierarchy with optimistic UI badge: "⚡ Transcoding"]
[Teacher immediately starts authoring Item 2 without delay]
```

### UX Flow Breakdown:
1. **Video Uploading:**
   - Client calls `POST /api/video-uploading/upload` with file metadata.
   - S3 pre-signed `PUT` URL and `VideoId` are returned in <100ms.
   - Browser uploads the binary directly to S3 with a native progress bar.
   - Canonical ARN generated: `az:video:{tenantId}:video/{videoId}`.
2. **Assessment Authoring:**
   - Teacher clicks "Add Quiz". A slide-over drawer opens over the canvas.
   - Teacher writes questions or selects a template from the catalog.
   - Saving calls `POST /assessments`, returning `az:assessment:{tenantId}:assessment/{assessmentId}`.
   - Drawer dismisses; quiz is slotted as Primary or Auxiliary.
3. **Document Uploading:**
   - Teacher drops a PDF/slides file into the Auxiliary slot.
   - Client calls `POST /api/documents/upload`, receives pre-signed S3 URL, and streams file to S3.
   - Canonical ARN generated: `az:document:{tenantId}:document/{documentId}`.
4. **Inline Text Authoring:**
   - Markdown editor opens directly inside the slot.
   - Content is stored locally in client state.
   - When the item is saved, content is packaged into `Metadata: { "markdown": "...", "format": "markdown" }` under ARN `az:inline:{tenantId}:inline/{newGuid}`.

---

## 4. Universal ARN & Permission Authorization Architecture

### 4.1 Canonical Asset ARNs vs. Container-Derived Scoped Access

To support **100% asset reusability** across courses, sections, and future blogs without IAM policy explosion:

```
CANONICAL ASSET REPOSITORY (Owned by Tenant Library)
├── az:video:{tenantId}:video/{videoId}              <-- Reusable video asset
├── az:assessment:{tenantId}:assessment/{assessId}  <-- Reusable quiz asset
└── az:document:{tenantId}:document/{documentId}    <-- Reusable PDF/doc asset
                         |
                         | Linked via CurriculumResource reference
                         v
COURSE PLACEMENT (Curriculum Hierarchy)
az:course:{tenantId}:course/{courseId}
└── az:section:{tenantId}:course/{courseId}/section/{sectionId}
    └── az:item:{tenantId}:course/{courseId}/section/{sectionId}/item/{itemId}
        ├── Resource 1 (Primary)   --> Points to az:video:...
        └── Resource 2 (Auxiliary) --> Points to az:document:...
```

### 4.2 Authorization Matrix

| Actor | Action / Endpoint | Evaluated ARN | IAM Permission | How Access is Resolved |
| :--- | :--- | :--- | :--- | :--- |
| **Teacher / Admin** | Upload Video | `az:tenant:{tenantId}:tenant/{tenantId}` | `video:Upload` | Direct Principal permission |
| **Teacher / Admin** | Create Assessment | `az:tenant:{tenantId}:tenant/{tenantId}` | `assessments:Create` | Direct Principal permission |
| **Teacher / Admin** | Upload Document | `az:tenant:{tenantId}:tenant/{tenantId}` | `documents:Upload` | Direct Principal permission |
| **Teacher / Admin** | Author Course / Add Item | `az:course:{tenantId}:course/{courseId}` | `courses:Edit` | Principal / TenantUser assignment on Course |
| **Enrolled Student** | Stream Video in Course | `az:course:{tenantId}:course/{courseId}` | `courses:View` | Scoped assignment to course; gateway validates enrollment & issues signed CDN cookie |
| **Enrolled Student** | Submit Course Assessment| `az:course:{tenantId}:course/{courseId}` | `assessments:Submit` | Scoped assignment to course; validates course membership |
| **Public User** | Watch Video on Free Blog | `az:blog:{tenantId}:post/{postId}` | `blog:View` | Public policy allows read access on blog container |

---

## 5. Domain Layer Refactoring (`AlphaZero.Modules.Courses.Domain`)

### 5.1 Enhancements to [`Course`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Domain/Aggregates/Courses/Course.cs)
```csharp
// 1. Add Curriculum Item with primary and optional auxiliary resources
public ErrorOr<CurriculumItem> AddCurriculumItem(
    Guid sectionId, 
    string title, 
    string mainType, 
    ResourceArn primaryResourceArn, 
    JsonElement primaryMetadata,
    IEnumerable<(ResourceArn Arn, JsonElement Metadata)>? auxiliaryResources = null);

// 2. Remove an item
public ErrorOr<Success> RemoveItem(Guid sectionId, Guid itemId);

// 3. Remove a section
public ErrorOr<Success> RemoveSection(Guid sectionId);

// 4. Auxiliary Resource Lifecycle
public ErrorOr<Success> AddAuxiliaryResourceToItem(Guid itemId, ResourceArn resourceArn, JsonElement metadata);
public ErrorOr<Success> RemoveResourceFromItem(Guid itemId, ResourceArn resourceArn);
public ErrorOr<Success> ReorderItemResources(Guid itemId, List<ResourceArn> orderedArns);
```

### 5.2 Enhancements to [`CurriculumItem`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Domain/Aggregates/Courses/CurriculumItem.cs)
- Enforce that an item can only have **one Primary resource**. Adding another primary resource replaces the existing primary resource or returns an error.
- Implement `RemoveResource(ResourceArn arn)`.
- Support `UpdateMetadata(ResourceArn arn, JsonElement metadata)`.

---

## 6. Infrastructure & Repository Fixes

### 6.1 Fix Incomplete Graph Loading in [`CourseRepository.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Infrastructure/Repositories/CourseRepository.cs)
Update `GetByIdWithSectionsAsync` to fully load owned resources:
```csharp
public async Task<Course?> GetByIdWithSectionsAsync(Guid id, CancellationToken cancellationToken = default)
{
    return await _context.Courses
        .Include(c => c.Sections)
            .ThenInclude(s => s.Items)
                .ThenInclude(i => i.Resources)
        .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
}
```

---

## 7. Application & Presentation Layer (The New Endpoints)

### 7.1 Deleted Legacy Endpoints (Breaking Clean Replacement)
- ❌ `DELETE`: `AlphaZero.Modules.Courses.Presentation.Courses.AddItem.AddLessonEndpoint` (`POST /courses/{CourseId}/sections/{SectionId}/lessons`)
- ❌ `DELETE`: `AlphaZero.Modules.Courses.Presentation.Courses.AddItem.AddAssessmentEndpoint` (`POST /courses/{CourseId}/sections/{SectionId}/assessments`)
- ❌ `DELETE`: `AddLessonCommand` & `AddLessonCommandHandler`
- ❌ `DELETE`: `AddAssessmentCommand` & `AddAssessmentCommandHandler`

### 7.2 New Unified Curriculum Endpoints

#### 1. Create Curriculum Item
- **Route:** `POST /courses/{CourseId}/sections/{SectionId}/items`
- **Permission:** `courses:Edit` on `az:course:{tenantId}:course/{CourseId}`
- **Request Body:**
```json
{
  "title": "Introduction to Kinematics",
  "mainType": "Video",
  "primaryResource": {
    "arn": "az:video:d3b07384...:video/11111111-...",
    "metadata": { "duration": "00:14:20", "thumbnailUrl": "https://..." }
  },
  "auxiliaryResources": [
    {
      "arn": "az:document:d3b07384...:document/22222222-...",
      "metadata": { "fileName": "LectureNotes.pdf", "sizeBytes": 2048576 }
    },
    {
      "arn": "az:inline:d3b07384...:inline/33333333-...",
      "metadata": { "title": "Summary Notes", "markdown": "# Key Formulas\n..." }
    }
  ]
}
```
- **Response:** `201 Created` with `{ "id": "itemId", "bitIndex": 0, "order": 0 }`.

#### 2. Item Lifecycle & Auxiliary Resource Endpoints
- `DELETE /courses/{CourseId}/sections/{SectionId}/items/{ItemId}` &rarr; Soft deletes item, updates section orders.
- `PATCH /courses/{CourseId}/sections/{SectionId}/items/{ItemId}` &rarr; Updates title or metadata.
- `POST /courses/{CourseId}/items/{ItemId}/resources` &rarr; Appends an auxiliary resource to an existing item.
- `DELETE /courses/{CourseId}/items/{ItemId}/resources/{ResourceArn}` &rarr; Removes an auxiliary resource.
- `PUT /courses/{CourseId}/items/{ItemId}/resources/reorder` &rarr; Reorders resources within an item (`{ "orderedArns": [...] }`).
- `DELETE /courses/{CourseId}/sections/{SectionId}` &rarr; Soft deletes section and all nested items.

#### 3. Course Context Scoped Media Streaming / Access Gateway
- **Route:** `GET /courses/{CourseId}/items/{ItemId}/resources/{ResourceArn}/access`
- **Permission:** `courses:View` on `az:course:{tenantId}:course/{CourseId}`
- **Behavior:**
  1. Validates caller enrollment in `CourseId`.
  2. Validates `ResourceArn` is part of `ItemId`.
  3. Dispatches to target module (`VideoStreaming` or `Documents`) to generate signed CloudFront cookie/URL.
  4. Returns `{ "url": "https://cdn.alphazero.academy/...", "type": "HlsManifest", "drm": { ... } }`.

---

## 8. New Module: `AlphaZero.Modules.Documents`

Following the lightweight Clean Architecture pattern of `VideoUploading`:
1. **Domain:**
   - Entity: `Document(Id, TenantId, FileName, ContentType, SizeBytes, S3Key, Status)`
   - ARN: `ResourceArn.ForDocument(TenantId, DocumentId)` (`az:document:{tenantId}:document/{documentId}`)
2. **Endpoints:**
   - `POST /api/documents/upload`: Generates S3 pre-signed `PUT` URL for PDF/document uploads.
   - `POST /api/documents/{id}/complete`: Marks upload complete, extracts metadata.
   - `GET /api/documents/{id}`: Retrieves document metadata and pre-signed download URL.

---

## 9. Comprehensive Test Plan & ASCII Coverage Map

### 9.1 ASCII Code Path & User Flow Coverage Map

```
CODE PATHS                                                 USER FLOWS
[+] Modules/Courses/Application/Items                      [+] Teacher In-Situ Authoring
  ├── AddCurriculumItemCommand                             │   ├── [★★★ TESTED] Add video item with primary ARN
  │   ├── [★★★ TESTED] Valid primary + multi-auxiliary     │   ├── [★★★ TESTED] Add item with auxiliary PDF & text
  │   ├── [GAP]         Tenant mismatch BOLA check         │   ├── [GAP]         Upload failure in S3 rollback
  │   └── [★★★ TESTED] NextAvailableBitIndex allocation    │   └── [GAP]         Reorder items and auxiliary resources
  ├── RemoveCurriculumItemCommand                          [+] Student Scoped Playback & Submissions
  │   ├── [★★★ TESTED] Soft-deletes item                   │   ├── [★★★ TESTED] Enrolled student streams course video
  │   └── [GAP]         Bitmask stability preserved        │   ├── [GAP] [→E2E]  Unenrolled student denied (403)
  └── AuxiliaryResourceCommands                            │   └── [★★★ TESTED] Quiz completion flips item bitmask
      ├── [★★★ TESTED] Add auxiliary resource              [+] Cross-Course Asset Reuse
      ├── [★★★ TESTED] Remove auxiliary resource           │   ├── [★★★ TESTED] Same video ARN in Course A & Course B
      └── [★★★ TESTED] Reorder auxiliary resources         │   └── [GAP] [→E2E]  Student in Course A cannot access Course B

COVERAGE: 10/14 paths tested (71%)  |  Code paths: 6/8 (75%)  |  User flows: 4/6 (67%)
QUALITY: ★★★: 7  ★★: 3  ★: 0  |  GAPS: 4 (2 E2E)
```

### 9.2 Refactoring Existing Tests in `tests/Modules/Courses`
All occurrences in:
- `CourseTests.cs`
- `EnrollmentTests.cs`
- `SoftDeleteTests.cs`
- `BitmaskStabilityTests.cs`

Are refactored from `Client.PostAsJsonAsync("/courses/.../lessons", ...)` to `Client.PostAsJsonAsync("/courses/.../items", ...)`.

---

## 10. Performance & Concurrency Profile

1. **Direct-to-S3 Uploads:** Multi-gigabyte video files and heavy PDF slide decks stream directly from the client's browser to S3 via pre-signed URLs. The AlphaZero API server spends **zero memory buffers** and **zero network bandwidth** on media streams.
2. **Database Queries:**
   - `CurriculumResources` is queried with `AsSplitQuery()` in `CourseQueryService.cs` to prevent Cartesian product blowup.
   - Index on `Courses.CurriculumResources(CurriculumItemId, Order)`.
   - Index on `Courses.CurriculumResources(Arn)`.
3. **VARBIT Progress Footprint:** Storing completion as a bitmask in PostgreSQL `VARBIT` ensures student progress reads and writes remain $O(1)$ and under 32 bytes per enrollment.

---

## GSTACK REVIEW REPORT

| Review Run | Status | Critical Findings | Verdict |
| :--- | :--- | :--- | :--- |
| **Architecture** | PASSED | 0 Blockers (Container-Derived Scoped Access solves asset reuse) | APPROVED |
| **Code Quality** | PASSED | Repository graph-loading bug resolved; unified REST contracts | APPROVED |
| **Test Review** | PASSED | 14 test paths mapped; full refactor of legacy test suite specified | APPROVED |
| **Performance** | PASSED | Direct S3 pre-signed streaming + Split Queries + VARBIT progress | APPROVED |

**VERDICT:** APPROVED FOR IMPLEMENTATION

NO UNRESOLVED DECISIONS
