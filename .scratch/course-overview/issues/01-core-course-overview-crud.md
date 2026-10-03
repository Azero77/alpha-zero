# 01: Core Course Overview CRUD (Text Fields)

**What to build:** The end-to-end behavior for managing the text content (Description, Target Audience, Learning Objectives) of a Course Overview. Tenant managers can edit this rich text content, and visitors can view it on published courses with proper caching.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The database schema has a `CourseOverview` table with three JSONB columns for content (`DescriptionContent`, `TargetAudienceContent`, `LearningObjectivesContent`).
- [ ] `PUT /courses/{id}/overview` allows a user with `courses.overview:Manage` permission to save JSON block content.
- [ ] Invalid JSON blocks or unknown block types are rejected with a validation error.
- [ ] `GET /courses/{id}/overview` returns the overview data for unauthenticated users, ONLY if the course is published.
- [ ] The GET endpoint returns `Cache-Control: public, max-age=300` and an `ETag`.
