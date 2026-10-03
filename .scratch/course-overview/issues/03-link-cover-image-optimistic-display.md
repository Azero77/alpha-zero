# 03: Link Cover Image & Optimistic Display in Overview

**What to build:** The Course Overview now supports a hero cover image. Managers can link an uploaded image, and the public page will immediately show the original upload, automatically upgrading to a fast-loading optimized variant once background processing finishes.

**Blocked by:** 01-core-course-overview-crud, 02-document-metadata-and-variants

**Status:** ready-for-agent

- [ ] The `CourseOverview` schema and `PUT` endpoint accept an optional `CoverImageDocumentId`.
- [ ] The system validates that the provided `CoverImageDocumentId` exists and belongs to the current tenant.
- [ ] The `GET /courses/{id}/overview` endpoint resolves the `CoverImageDocumentId` using the Documents module.
- [ ] The `GET` endpoint returns the best available image URL (optimistic display: original if pending, hero variant if ready).
