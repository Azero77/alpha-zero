# 03: Link Cover Image in Overview

**What to build:** The Course Overview now supports a hero cover image. Managers can link an uploaded image. The public page will show a processing indicator until the optimized hero variant is fully ready in the `processed-documents` bucket.

**Blocked by:** 01-core-course-overview-crud, 02-document-metadata-and-variants

**Status:** ready-for-agent

- [ ] The `CourseOverview` schema and `PUT` endpoint accept an optional `CoverImageDocumentId`.
- [ ] The system validates that the provided `CoverImageDocumentId` exists and belongs to the current tenant.
- [ ] The `GET /courses/{id}/overview` endpoint resolves the `CoverImageDocumentId` using the Documents module.
- [ ] The `GET` endpoint returns the best available image URL (or a processing state if pending, dropping optimistic display of raw uploads).
