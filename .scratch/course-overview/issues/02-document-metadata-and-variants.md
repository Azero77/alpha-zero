# 02: Document Metadata, Status, and Variant Tracking

**What to build:** Enhancements to the Documents module to track the processing lifecycle of uploaded images. Clients can specify how an image will be used during upload, and the system can track when the optimized versions of that image are ready.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The `Document` database schema tracks a `Status` (Pending/Ready/Failed) and a list of `Variants` (e.g., URL, width, height).
- [ ] The `POST /documents` upload API accepts an optional usage context (e.g., `CourseCover`, `CourseInline`).
- [ ] The API attaches this usage context as S3 object metadata (`x-amz-meta-usagecontext`) when uploading the raw file.
- [ ] The `GET /documents/{id}` query returns the best available URL (a processed variant if `Ready`, or the original URL if `Pending`).
