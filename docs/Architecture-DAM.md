# Digital Asset Management (DAM) Architecture

This document outlines the architecture for the `Documents` module (and asset management across the platform), heavily inspired by the proven patterns of the **Squidex Headless CMS**. We have adapted Squidex's rich metadata extraction and event-driven design to fit securely into AlphaZero's Enterprise Clean Architecture (EF Core, MassTransit Sagas, and Serverless).

## 1. The Domain Entity (`Document`)

Unlike simple file-upload systems, a true DAM needs to support rich metadata, deduplication, and various asset types. We model our `Document` entity based on the Squidex `Asset` pattern.

### Key Characteristics:
- **Deduplication:** Evaluated server-side post-upload via `FileHash` to prevent storing the same heavy media multiple times while avoiding client-side hash spoofing vulnerabilities.
- **Dynamic Metadata:** Uses a `Dictionary<string, object> Metadata` mapped to a `jsonb` column in EF Core instead of hardcoded schema fields.
- **Categorization:** Uses a `DocumentType` enum (`Image`, `Video`, `Audio`, `Pdf`, `Document`, `Unknown`) to drive frontend rendering.
- **Saga Status:** Tracks its processing state via a `DocumentStatus` enum (`Pending`, `Processing`, `Ready`, `Faulted`).

## 2. Image Optimization & Variants Pipeline

All media processing (including images) is strictly offloaded to serverless architectures (AWS Step Functions) to protect the core API from memory exhaustion (OOM).

When an image is processed, the Lambda generates multiple variants rather than a single file:
1. **Large (WebP, ~1920w):** Used for Course Covers and Hero banners on desktop.
2. **Medium (WebP, ~800w):** The default for inline lesson images to maintain text clarity.
3. **Small (WebP, ~300w):** For lists, catalog grids, and mobile thumbnails.
4. **BlurHash:** A tiny (~20 char) string generated and stored in the `Metadata` JSONB column.

**Benefit:** The frontend uses the BlurHash and exact dimensions to render a blurry placeholder instantly, completely eliminating Cumulative Layout Shift (CLS) on slow connections.

## 3. Hybrid Cloud Economics: AWS Compute + Cloudflare R2 Delivery

To prevent bandwidth bankruptcy (egress costs for HLS streaming), we employ a hybrid cloud architecture.

1. **Ingestion & Processing (AWS):** Users upload to a `raw-assets` AWS S3 bucket. AWS Step Functions and AWS Elemental MediaConvert execute the heavy transcoding and optimization, outputting to a temporary `processed-assets` AWS bucket.
2. **The Handoff:** A Lambda function immediately syncs the processed files to a **Cloudflare R2** bucket via its S3-compatible API, then deletes the AWS copy. Raw files in AWS are purged automatically via S3 Lifecycle Policies.
3. **Delivery (Cloudflare R2):** Students stream the videos and images from Cloudflare R2, which charges **$0 for egress bandwidth**.
4. **Security & Access Control:** Because the output bucket is private, a **Cloudflare Worker** sits in front of the R2 bucket. When a student requests a lesson, the API issues them a JWT. The Worker intercepts asset requests, verifies the JWT signature in milliseconds, and streams the media.

## 4. Integration with Course Lessons (Block-Based Rich Text)

Following standard Headless CMS paradigms, course lessons are NOT stored as monolithic HTML or Markdown strings (where S3 URLs would be hardcoded and brittle).

Instead, lessons in the `Courses` module are stored as a JSONB array of Blocks (integrated via `CurriculumItem`):
```json
[
  { "type": "markdown", "data": { "text": "## Chapter 1" } },
  { "type": "image", "data": { "documentId": "uuid-from-dam", "blurHash": "...", "width": 800 } }
]
```
Images and videos sit **outside** the markdown. The frontend (e.g., TipTap or Editor.js) uses the `documentId` to query the DAM for the latest Cloudflare R2 secure URL. This ensures absolute referential integrity and allows the backend to track exactly which lessons depend on which DAM assets.

## 5. MassTransit Saga State Machine

Squidex uses strict Event Sourcing for state transitions. In AlphaZero, we achieve this scalable, event-driven state transition using **MassTransit Saga State Machines** backed by EF Core.

The `DocumentProcessingSaga` tracks the entire lifecycle of an upload:
1. **`UploadInitiated`:** The client requests a presigned URL. The Saga starts, creates a `Pending` document, and schedules a **24-hour timeout** (abandoning the saga if the user never uploads the file).
2. **`UploadedToStorage`:** S3 triggers an event via SQS. The Saga transitions to `Processing` and triggers the AWS Step Functions / Metadata Pipeline.
3. **`ProcessingCompleted`:** The serverless pipeline finishes. The Saga populates the final `Metadata` dictionary, marks the document `Ready`, and finalizes.
4. **`Faulted`:** Any errors transition the document to `Faulted`, scheduling a hard deletion of both the DB record and orphaned S3/R2 blobs after 1 day to prevent storage leaks.

## 6. Multi-Tenancy & Authorization (The Context Pattern)

Instead of injecting an `ITenantProvider` deep into domain entities, we adopt Squidex's **Immutable Context** pattern for executing commands.

Every API request generates an immutable `Context` object containing:
- The current `TenantId`.
- The User's `ClaimsPrincipal`.
- Calculated `Permissions`.

This context is passed through the Command bus, ensuring that deep domain logic (e.g., "Does this user have quota to upload a 5GB video?") can be evaluated securely with absolutely zero risk of async thread-local state leakage.
