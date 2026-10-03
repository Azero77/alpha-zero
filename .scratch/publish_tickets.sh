#!/bin/bash
set -e

gh label create "ready-for-agent" -c "#0e8a16" --force 2>/dev/null || true

create_issue() {
    local title="$1"
    local body="$2"
    url=$(gh issue create --title "$title" --body "$body" --label "ready-for-agent")
    issue_number=$(echo "$url" | awk -F'/' '{print $NF}')
    repo=$(gh repo view --json nameWithOwner --jq .nameWithOwner)
    db_id=$(gh api "repos/$repo/issues/$issue_number" --jq .id)
    echo "$issue_number|$db_id|$url"
}

add_dependency() {
    local child_num="$1"
    local blocker_db_id="$2"
    repo=$(gh repo view --json nameWithOwner --jq .nameWithOwner)
    gh api --method POST "repos/$repo/issues/$child_num/dependencies/blocked_by" -F "issue_id=$blocker_db_id" 2>/dev/null || true
}

echo "Publishing tickets..."

# Ticket 1
T1_BODY=$(cat << 'BODY_EOF'
## What to build
The end-to-end behavior for managing the text content (Description, Target Audience, Learning Objectives) of a Course Overview. Tenant managers can edit this rich text content, and visitors can view it on published courses with proper caching.

## Acceptance criteria
- [ ] The database schema has a `CourseOverview` table with three JSONB columns for content (`DescriptionContent`, `TargetAudienceContent`, `LearningObjectivesContent`).
- [ ] `PUT /courses/{id}/overview` allows a user with `courses.overview:Manage` permission to save JSON block content.
- [ ] Invalid JSON blocks or unknown block types are rejected with a validation error.
- [ ] `GET /courses/{id}/overview` returns the overview data for unauthenticated users, ONLY if the course is published.
- [ ] The GET endpoint returns `Cache-Control: public, max-age=300` and an `ETag`.

## Blocked by
- None (can start immediately).
BODY_EOF
)
t1_info=$(create_issue "Core Course Overview CRUD (Text Fields)" "$T1_BODY")
t1_num=$(echo "$t1_info" | cut -d'|' -f1)
t1_db_id=$(echo "$t1_info" | cut -d'|' -f2)
t1_url=$(echo "$t1_info" | cut -d'|' -f3)
echo "Ticket 1 created: $t1_url"

# Ticket 2
T2_BODY=$(cat << 'BODY_EOF'
## What to build
Enhancements to the Documents module to track the processing lifecycle of uploaded images. Clients can specify how an image will be used during upload, and the system can track when the optimized versions of that image are ready.

## Acceptance criteria
- [ ] The `Document` database schema tracks a `Status` (Pending/Ready/Failed) and a list of `Variants` (e.g., URL, width, height).
- [ ] The `POST /documents` upload API accepts an optional usage context (e.g., `CourseCover`, `CourseInline`).
- [ ] The API attaches this usage context as S3 object metadata (`x-amz-meta-usagecontext`) when uploading the raw file.
- [ ] The `GET /documents/{id}` query returns the best available URL (a processed variant if `Ready`, or the original URL if `Pending`).

## Blocked by
- None (can start immediately).
BODY_EOF
)
t2_info=$(create_issue "Document Metadata, Status, and Variant Tracking" "$T2_BODY")
t2_num=$(echo "$t2_info" | cut -d'|' -f1)
t2_db_id=$(echo "$t2_info" | cut -d'|' -f2)
t2_url=$(echo "$t2_info" | cut -d'|' -f3)
echo "Ticket 2 created: $t2_url"

# Ticket 3
T3_BODY=$(cat << BODY_EOF
## What to build
The Course Overview now supports a hero cover image. Managers can link an uploaded image, and the public page will immediately show the original upload, automatically upgrading to a fast-loading optimized variant once background processing finishes.

## Acceptance criteria
- [ ] The \`CourseOverview\` schema and \`PUT\` endpoint accept an optional \`CoverImageDocumentId\`.
- [ ] The system validates that the provided \`CoverImageDocumentId\` exists and belongs to the current tenant.
- [ ] The \`GET /courses/{id}/overview\` endpoint resolves the \`CoverImageDocumentId\` using the Documents module.
- [ ] The \`GET\` endpoint returns the best available image URL (optimistic display: original if pending, hero variant if ready).

## Blocked by
- #$t1_num
- #$t2_num
BODY_EOF
)
t3_info=$(create_issue "Link Cover Image & Optimistic Display in Overview" "$T3_BODY")
t3_num=$(echo "$t3_info" | cut -d'|' -f1)
t3_db_id=$(echo "$t3_info" | cut -d'|' -f2)
t3_url=$(echo "$t3_info" | cut -d'|' -f3)
echo "Ticket 3 created: $t3_url"
add_dependency "$t3_num" "$t1_db_id"
add_dependency "$t3_num" "$t2_db_id"

# Ticket 4
T4_BODY=$(cat << BODY_EOF
## What to build
A background processing pipeline that automatically scales and optimizes uploaded images into thumbnail, hero, and content-width sizes without slowing down the main API server.

## Acceptance criteria
- [ ] A new AWS Lambda (\`ImageProcessor\`) is triggered by S3 PutObject events.
- [ ] The Lambda reads the S3 object metadata (\`x-amz-meta-usagecontext\`) to decide which sizes to generate.
- [ ] The Lambda generates the correct image variants and uploads them to a processed prefix in S3.
- [ ] The Lambda publishes an \`ImageProcessedEvent\` to SQS containing the variant URLs.
- [ ] A MassTransit consumer in the monolith receives the event and updates the \`Document\` record to \`Status = Ready\` with the variant URLs.

## Blocked by
- #$t2_num
BODY_EOF
)
t4_info=$(create_issue "Serverless Image Processing Pipeline (Lambda & Consumer)" "$T4_BODY")
t4_num=$(echo "$t4_info" | cut -d'|' -f1)
t4_db_id=$(echo "$t4_info" | cut -d'|' -f2)
t4_url=$(echo "$t4_info" | cut -d'|' -f3)
echo "Ticket 4 created: $t4_url"
add_dependency "$t4_num" "$t2_db_id"

echo "DONE"
