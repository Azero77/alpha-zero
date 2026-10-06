---
labels: ["ready-for-agent"]
---

## Problem Statement

When instructors upload documents (like syllabus PDFs or images), the system currently lacks a way to strictly tie those documents to a specific authorization scope (such as a specific course). Furthermore, when students download these documents, using traditional cookies to authorize every document request is inefficient and causes cookie-bloat in the browser, especially when students have access to multiple courses. 

## Solution

To secure document downloads without bloating HTTP request cookies, we will implement a "Master Course Token" architecture. 
Documents will be assigned a `Scope` upon upload (e.g., `course/{courseId}`). The S3/R2 storage path will incorporate this scope. 
When a student accesses a lesson, the backend Identity module will evaluate their IAM permissions for that scope and issue a single, cryptographically signed HMAC token for the entire scope. The frontend will append this token to document URLs, and the Cloudflare Edge CDN will validate the token to allow the download, completely bypassing the backend for the actual file fetch.

## User Stories

1. As an instructor, I want my uploaded documents to be assigned to a specific scope (like a course), so that they are logically organized and secured.
2. As an instructor, I want the storage paths of my documents to reflect their scope, so that tenant data remains isolated and organized in the bucket.
3. As a student, I want to be able to download multiple documents within a course seamlessly, so that my learning experience is uninterrupted.
4. As a student, I want my browser to remain performant and not crash due to excessive cookie header sizes, so that I can enroll in as many courses as I want.
5. As an administrator, I want document access to be strictly enforced by the AlphaZero IAM engine, so that unauthorized users cannot download premium course materials.
6. As a DevOps engineer, I want document authorization to happen at the Edge (Cloudflare), so that the core backend API is not overwhelmed by media download requests.
7. As a frontend developer, I want to receive a single token for a course payload, so that I can simply append it as a query parameter to all document URLs rather than managing complex cookie scopes.

## Implementation Decisions

- **Documents Module (Domain & Application)**:
  - Add a `Scope` string property to the `Document` model.
  - Modify `UploadDocumentCommand` to accept `Scope` from the client.
  - **Security Rule:** Enforce strict regex validation (`^[a-zA-Z0-9_-]+/[a-zA-Z0-9_-]+$`) on the `Scope` property using FluentValidation to prevent path traversal or malformed paths.
  - Update `DocumentFileStorageConstants` to use the new pattern: `documents/{tenantId}/{scope}/{documentId}/{fileName}`.
  - The `Scope` must follow the `ResourceArn` conventions (specifically the `ResourcePath` segment, e.g., `course/{courseId}`).

- **Identity Module (IAM Endpoint)**:
  - Create a new MediatR query `GenerateDocumentTokenQuery` that accepts a `Scope`.
  - The handler must evaluate the IAM engine against the requested ARN (e.g., `az:course:{tenantId}:{scope}`). (Note: ensure ARN properly formats as `az:course:{tenantId}:course/{courseId}` per `ResourceArn.ForCourse`).
  - If authorized, generate an HMAC-SHA256 signed payload. The payload should include `{"path": "/documents/{tenantId}/{scope}/", "exp": <unix-timestamp>}`.
  - **Token TTL:** Set the `exp` timestamp to **1 hour** from current time (balances edge offloading with a reasonable revocation window).
  - The secret used to sign the HMAC token must match `DOCUMENT_HMAC_SECRET` expected by the Cloudflare Worker.

- **Infrastructure / Pipeline**:
  - Ensure the `ImagePipelineConstruct` (or any document ingestion pipeline) correctly maps the new `scope` property from the `Document` DB record or the S3 event, if necessary for processing. Since the S3 key inherently contains the scope now, parsers might need to correctly parse the new S3 key structure to extract `tenantId`, `scope`, and `documentId`.

## Testing Decisions

- **What makes a good test**: Tests should focus on the external API/MediatR behavior. We will not test the internal Cloudflare edge logic in the backend test suite, nor the internal IAM condition evaluation engine directly here (as IAM is tested in its own suite).
- **Modules to be tested**:
  - `Documents.UnitTests` (or Integration Tests): Verify that `UploadDocumentCommand` successfully persists the `Scope` to the database and generates the correct S3 Key structure.
  - `Identity.IntegrationTests`: Verify that `GenerateDocumentTokenQuery` returns a valid JWT/HMAC token when a user has valid permissions to the requested scope ARN. Verify it returns a `Forbidden` or Unauthorized error when the user lacks permissions.
- **Prior art**: Look at existing `DocumentTests.cs` for entity creation tests, and existing IAM endpoint tests for authorization assertions.

## Out of Scope

- The Cloudflare Worker code (this has already been written in `infrastructure/cloudflare/src/index.js`).
- Modifying the Video uploading module (videos will continue to use their own cookie-based mechanism).
- Frontend UI implementation for appending the `?token=` query parameter (this will be handled by the frontend team in a separate ticket).

## Further Notes

- The token format being generated by the Identity module must precisely match the format expected by the Cloudflare worker (`Base64Url(JSON).SignatureHex`), using constant-time comparison on the edge.
