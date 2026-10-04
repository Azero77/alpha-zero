# 02: Document Upload & Saga State Tracking

**What to build:** Enhancements to the Documents module to initiate a document upload, record it, and set up the foundation for Saga state tracking (for multiple document types like images, PDFs, DOCX).

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The `Document` database schema tracks multiple document types (not just images).
- [ ] Implement a MassTransit Saga State Machine (`DocumentProcessingState` using Entity Framework) to track the detailed steps of processing. This state should be designed to be deleted once the document processing is successfully completed.
- [ ] `POST /documents/upload` generates a presigned URL targeting a `raw-documents` S3 bucket, saves a `Document` record in a `Pending` state, and prepares necessary metadata (`UsageContext`, `TenantId`, etc.).
- [ ] `GET /documents/{id}` no longer does optimistic display. If the document is `Pending`, it returns a pending/processing indicator instead of the raw URL.
