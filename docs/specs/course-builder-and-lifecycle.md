# 🪐 Course Lifecycle & Item Builder Specification

## 1. Executive Summary
This document specifies the technical architecture, domain rules, and FastEndpoints contracts for:
1. Course Inception & Automatic Overview Provisioning
2. Syrian/MENA "Sync-Day" Batch Asset Ingestion & Multi-Use Pool
3. Course Section Item Builder API (Composite JSONB Blocks)
4. `doc://` Dual-Origin DAM Markdown Convention
5. Course Readiness Gates & Role-Based Direct Publishing

---

## 2. Course Inception & Overview Relationship

### 2.1 Two-Stage Initialization
- Calling `POST /courses` creates the `Course` aggregate in `Draft` state.
- During command execution, the handler automatically seeds an empty `CourseOverview` record linked by `CourseId`.
- This eliminates 404 race conditions when a teacher opens the Overview editor tab immediately after creating the course.

### 2.2 Short Teaser vs. Full Rich Syllabus
- `Course.Title` and `Course.Description` (plain string) provide lightweight data for course cards, search catalogs, and mobile list views.
- `CourseOverview` stores the full ProseMirror/Tiptap rich-text blocks:
  - `DescriptionContent` (JSON)
  - `TargetAudienceContent` (JSON, optional)
  - `LearningObjectivesContent` (JSON, optional)
  - `CoverImageDocumentId` (Guid, optional, referencing public DAM document)

---

## 3. Syrian/MENA "Sync-Day" Batch Ingestion & Multi-Use Asset Pool

### 3.1 Offline Realities & Batch Upload
Instructors batch-upload video lectures and documents on scheduled days with stable electrical grid and Internet access.
- Videos are uploaded to the Videos pipeline (`AlphaZero.Modules.VideoUploading`). MediaConvert outputs HLS playlists and emits `VideoMetadataChangedIntegrationEvent`.
- Documents/Images are uploaded to DAM (`AlphaZero.Modules.Documents`). Step Functions generate WebP variants and emit `DocumentMetadataChangedIntegrationEvent`.
- Courses consumers (`VideoMetadataChangedConsumer`, `DocumentMetadataChangedConsumer`) project these assets into `CourseAsset` records under the course pool in `Available` status.

### 3.2 Reference Counting & Multi-Use Assets
A single video or formula sheet may be attached to multiple lessons across the curriculum without re-uploading.
- `CourseAsset.RefCount` tracks active curriculum references.
- Attaching to a section item increments `RefCount` and transitions `State` to `InUse`.
- Removing an item or editing blocks decrements `RefCount`.
- When `RefCount == 0`, `State` returns to `Available`.
- Deleting an asset from the pool is prohibited if `RefCount > 0`.

---

## 4. Course Section Item Builder API

### 4.1 Endpoint Contracts
- **Create Item**: `POST /courses/{CourseId}/sections/{SectionId}/items`
- **Update Item**: `PUT /courses/{CourseId}/sections/{SectionId}/items/{ItemId}`
- **Delete Item**: `DELETE /courses/{CourseId}/sections/{SectionId}/items/{ItemId}`

### 4.2 Request Schema
```json
{
  "title": "Newton's First Law of Motion",
  "mainType": "Video",
  "content": [
    {
      "type": "video",
      "courseAssetId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "videoId": "8a7c3e12-429a-41df-9c02-e2d193f412aa"
    },
    {
      "type": "inline",
      "tiptap": {
        "type": "doc",
        "content": [
          {
            "type": "paragraph",
            "content": [
              {
                "type": "text",
                "text": "Review the inertia formula in the diagram below:"
              }
            ]
          }
        ]
      }
    },
    {
      "type": "document",
      "courseAssetId": "4c92b231-9214-4a21-9921-b0e29381cbb4",
      "documentId": "1b32f111-9214-4a21-9921-b0e29381cbb4",
      "documentType": "pdf",
      "title": "Inertia Worksheet.pdf"
    },
    {
      "type": "assessment",
      "courseAssetId": "5fa85f64-5717-4562-b3fc-2c963f66afa6",
      "assessmentId": "77a85f64-5717-4562-b3fc-2c963f66afa6",
      "passingScore": 80
    }
  ]
}
```

### 4.3 Domain Invariants
- `MainType` is required (`Video`, `Document`, `Article`, `Quiz`). It governs UI icon badges and curriculum hierarchy.
- `BitIndex` is strictly immutable. It is assigned monotonically on creation (`Course.NextAvailableBitIndex++`).
- Item deletion performs a soft delete (`IsDeleted = true`). The `BitIndex` is permanently burned to prevent shifting existing student `VARBIT` progress masks.

---

## 5. `doc://` Markdown URI Convention

### 5.1 URI Format
```
doc://{public|private}/{documentId}?variantName={variantName}
```
- `{public|private}`: Specifies whether the asset is in the public Cloudflare R2 bucket or private S3 bucket.
- `{documentId}`: The UUID of the document in the DAM module.
- `variantName`: Optional image variant produced by Step Functions (`thumb`, `card`, `full`, or `raw`).

### 5.2 Client Resolution
- **Public Assets (`doc://public/...`)**:
  Frontend substitutes the tenant CDN template without calling any backend endpoint:
  ```
  https://{cdnDomain}/{tenantId}/{documentId}/{variantName}.webp
  ```
  Zero authentication overhead, fully cached at the Cloudflare edge.
- **Private Assets (`doc://private/...`)**:
  Frontend calls `GET /documents/{documentId}/download-url?variantName={variantName}` to receive a short-lived S3 pre-signed URL (6-hour validity).

---

## 6. Course Lifecycle & Publishing Quality Gates

### 6.1 State Machine
`Draft` -> `UnderReview` -> `Approved` -> `Published` (or `Archived`)

### 6.2 Readiness Checklist Endpoint
- **Route**: `GET /courses/{CourseId}/readiness`
- **Response**:
```json
{
  "canPublish": false,
  "canSubmitForReview": false,
  "checks": {
    "overview": {
      "hasTitle": true,
      "hasSubject": true,
      "hasDescription": true,
      "hasCoverImage": true
    },
    "curriculum": {
      "hasSections": true,
      "hasItems": true,
      "allAttachedAssetsReady": false
    },
    "commercial": {
      "hasActivePricingPlan": true
    }
  },
  "blockingReasons": [
    "Video asset 'Kinematics Lecture 1' is still transcoding."
  ]
}
```

### 6.3 Fast-Path Direct Publishing
- **Route**: `PATCH /courses/{CourseId}/publish`
- If caller possesses `courses:Publish` (TenantAdmin / Academy Owner):
  - Can publish directly from `Draft` state provided all readiness checks pass.
- If caller possesses `courses:Submit` (Teacher):
  - Must call `PATCH /courses/{CourseId}/review` to transition to `UnderReview`.
  - A TenantAdmin approves via `PATCH /courses/{CourseId}/approve`, after which publishing is unlocked.

---

## 7. Progress Tracking & Completion Logic

### 7.1 Multi-Resource Lesson Evaluation
When an item contains multiple components (Video + Quiz):
- Video watch threshold: `>= 80%` of duration.
- Quiz: Student must receive a passing score.
- The item completion event (`ItemCompletedDomainEvent`) fires only when **all present criteria** are satisfied, flipping the item's bit in the enrollment bitmask (`VARBIT`) to `1`.
