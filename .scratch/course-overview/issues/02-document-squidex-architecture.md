# 02: Document Architecture Rebuild (Squidex DAM Pattern)

**What to build:** Rebuild the core of the Documents module to act as a highly scalable Digital Asset Management (DAM) system. This adapts the best patterns from Squidex (Rich Metadata, Metadata Source Pipeline) and marries them to our Enterprise Clean Architecture (MassTransit Sagas, EF Core).

**Blocked by:** None

**Status:** ready-for-agent

## 1. Domain Model Updates (`Document.cs`)
- [ ] Refactor the `Document` entity to mimic the Squidex `Asset` approach:
  - Add properties for `FileName`, `MimeType`, `FileSize`, and `FileHash` (for deduplication).
  - Add `DocumentType Type` enum (`Unknown`, `Image`, `Video`, `Audio`, `Pdf`, `Document`).
  - Add a `Dictionary<string, object> Metadata` property, mapped as a `jsonb` column in EF Core.
  - Update `DocumentStatus Status` (`Pending`, `Processing`, `Ready`, `Faulted`).

## 2. MassTransit Saga (`DocumentProcessingSaga`)
- [ ] Implement a MassTransit Saga (`DocumentProcessingState` & `DocumentProcessingSaga`) using Entity Framework Core.
- [ ] Handle the `UploadInitiated` event: Transition to `Processing` and schedule a 24-hour abandonment timeout.
- [ ] Handle the `UploadedToStorage` event (triggered by the `raw-documents` S3 bucket).
- [ ] Ensure the Saga properly finalizes and deletes the SQL row upon completion.

## 3. Metadata Extraction Pipeline (`IDocumentMetadataSource`)
- [ ] Define the `IDocumentMetadataSource` interface and `DocumentUploadContext`.
- [ ] Implement a pipeline that runs when a file is uploaded, chaining multiple metadata sources to populate the `Document.Metadata` dictionary.
- [ ] *Note: Heavy extractions (like Video duration) can be handled by AWS Step Functions, but the API should have a pipeline to absorb and shape that metadata exactly like Squidex does.*

## 4. API Updates
- [ ] Update `POST /documents/upload` to issue a presigned URL targeting the `raw-documents` bucket, create the `Pending` Document, and publish the `UploadInitiated` event to kick off the Saga.
