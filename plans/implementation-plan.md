# Implementation Plan: Unified Curriculum Engine, Course Resource Pool & Contextual Asset Authorization

**Platform:** AlphaZero Learning Academy  
**Status:** Phase 1 & Phase 2 Complete. Ready for Phase 3.

---

## Completed Phases

- [x] **Phase 1: Foundation Fixes**
  - Decoupled `Progress.ActiveItems` from bitmask capacity `TotalItems`.
  - Added `ActiveTrackedItems` calculation to `Course.cs`.
  - Made `CurriculumItem.GetExpectedServiceForMainType` exhaustive.
  - All 27 `Courses.UnitTests` passing.
- [x] **Phase 2: Documents Module (`AlphaZero.Modules.Documents`)**
  - Scaffolded Clean Architecture module (Domain, IntegrationEvents, Application, Infrastructure, Presentation).
  - Presigned S3 upload, download, metadata query, and list endpoints.
  - All 9 `Documents.UnitTests` passing.

---

## Phase 3: Unified Curriculum API & Course Resource Pool

Phase 3 introduces the unified curriculum command and the **Course Resource Pool (Staging Tray)**.

### 3.1 Course Resource Pool (`Course.cs` Aggregate Updates)
**File:** [`src/alphazero-api/Modules/Courses/Domain/Aggregates/Courses/Course.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Domain/Aggregates/Courses/Course.cs)

Add `CourseResourcePoolItem` and pool lifecycle methods:

```csharp
public class CourseResourcePoolItem
{
    public Guid Id { get; private set; }
    public ResourceArn Arn { get; private set; }
    public string Title { get; private set; }
    public JsonElement Metadata { get; private set; }
    public DateTime AddedOn { get; private set; }

    internal CourseResourcePoolItem(Guid id, ResourceArn arn, string title, JsonElement metadata, DateTime addedOn)
    {
        Id = id;
        Arn = arn;
        Title = title;
        Metadata = metadata;
        AddedOn = addedOn;
    }
}
```

In `Course.cs`:
```csharp
    private readonly List<CourseResourcePoolItem> _resourcePool = new();
    public IReadOnlyCollection<CourseResourcePoolItem> ResourcePool => _resourcePool.AsReadOnly();

    // 1. Ingestion: Transcoded video or document lands in course staging pool
    public ErrorOr<Success> AddResourceToPool(ResourceArn arn, string title, JsonElement metadata)
    {
        if (Status == CourseStatus.Published)
            return Error.Conflict("Course.Published", "Cannot add resources to published course pool.");

        if (_resourcePool.Any(p => p.Arn == arn))
            return Result.Success; // Idempotent

        _resourcePool.Add(new CourseResourcePoolItem(Guid.NewGuid(), arn, title, metadata, DateTime.UtcNow));
        return Result.Success;
    }

    // 2. Assignment: Teacher claims video from pool into a section
    public ErrorOr<CurriculumItem> AssignFromPool(Guid poolItemId, Guid sectionId, string title)
    {
        var poolItem = _resourcePool.FirstOrDefault(p => p.Id == poolItemId);
        if (poolItem is null)
            return Error.NotFound("Pool.ItemNotFound", "Resource not found in course pool.");

        var itemResult = AddCurriculumItem(sectionId, title, "Video", poolItem.Arn, poolItem.Metadata);
        if (itemResult.IsError) return itemResult.Errors;

        _resourcePool.Remove(poolItem);
        return itemResult;
    }

    // 3. Unassigning: Lesson removed from section -> returns resource back to pool
    public ErrorOr<Success> UnassignItemToPool(Guid itemId)
    {
        var section = _sections.FirstOrDefault(s => s.Items.Any(i => i.Id == itemId));
        if (section is null) return Error.NotFound("Section.NotFound");

        var item = section.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null) return Error.NotFound("Item.NotFound");

        var primary = item.Resources.FirstOrDefault(r => r.Type == "Primary");
        if (primary is not null)
        {
            _resourcePool.Add(new CourseResourcePoolItem(
                Guid.NewGuid(), primary.Arn, item.Title, primary.Metadata, DateTime.UtcNow));
        }

        item.SoftDelete();
        return Result.Success;
    }

    // 4. Dismissal: Remove unused video from course pool (retains it in tenant library)
    public ErrorOr<Success> DismissFromPool(Guid poolItemId)
    {
        var item = _resourcePool.FirstOrDefault(p => p.Id == poolItemId);
        if (item is null) return Error.NotFound("Pool.ItemNotFound");

        _resourcePool.Remove(item);
        return Result.Success;
    }
```

#### Publishing Invariant:
In `Course.Publish()`:
```csharp
    if (_resourcePool.Any())
    {
        return Error.Conflict("Course.ResourcePoolNotEmpty", 
            "Cannot publish course while unassigned items remain in the resource pool. Assign them to lessons or dismiss them.");
    }
```

### 3.2 Refactor Consumer: `AddVideoToCoursePoolConsumer`
**File:** `Modules/Courses/Infrastructure/Consumers/Videos/AddVideoToCoursePoolConsumer.cs`  
(Replaces `AutoLinkVideoToCourseConsumer.cs`)

When `VideoPublishedEvent` is received:
- Extracts `courseId` from `TargetResourceArn`.
- Calls `course.AddResourceToPool(videoArn, videoTitle, metadata)`.
- Video is now safely staged in the course pool, immune to any section renaming/reordering.

### 3.3 Unified Item Creation & Pool Endpoints
- **Delete Legacy Endpoints:**
  - `Modules/Courses/Presentation/Courses/AddItem/AddLesson.cs`
  - `Modules/Courses/Presentation/Courses/AddItem/AddQuiz.cs`
- **New Endpoints:**
  - `POST /courses/{courseId}/sections/{sectionId}/items` (Direct ARN attach)
  - `POST /courses/{courseId}/pool/{poolItemId}/assign` (Assign from pool)
  - `DELETE /courses/{courseId}/items/{itemId}/unassign` (Remove from curriculum, return to pool)
  - `DELETE /courses/{courseId}/pool/{poolItemId}/dismiss` (Dismiss from pool to tenant library)
  - `GET /courses/{courseId}/pool` (List staged items in course pool)

---

## Phase 4: Contextual ACL & Cloudflare Free CDN Streaming

### 4.1 Cloudflare CDN Topology
Cloudflare CDN provides **100% free egress caching** for video streaming:
- Master manifests (`.m3u8`) and encrypted HLS segments (`.ts`) are cached on Cloudflare edge.
- Delivery is secured with **ClearKey / AES-128 DRM**:
  - Segments are encrypted.
  - The browser requests decryption keys via `/api/video/keys/{videoId}?courseId={courseId}`.
- Uses existing [`DatabaseCloudFlareCdnVideoStreamingService`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/VideoUploading/Infrastructure/Streaming/CdnVideoStreamingService.cs#L23) in `VideoUploading`.

### 4.2 The Contextual ACL Service (`ICourseResourceAclService`)
When student plays a video:
1. Calls `GET /api/video/{videoId}?courseId={courseId}`.
2. ACL verifies that `Course(courseId)` contains `videoId` in its active curriculum items.
3. Synthesizes contextual ARN: `az:video:{tenantId}:course/{courseId}/video/{videoId}`.
4. FastEndpoints IAM verifies that student holds `az:*:{tenantId}:course/{courseId}/*`.
5. Returns Cloudflare CDN manifest URL + License URL:
   ```json
   {
     "url": "https://cdn.alphazero.com/streaming/{videoId}/master.m3u8",
     "encryptionMethod": "ClearKey",
     "licenseUrl": "/api/video/keys/{videoId}?courseId={courseId}"
   }
   ```
6. Key endpoint validates same contextual IAM before providing decryption key.

---

## Phase 5: Tenant Asset Catalog (Asset Library Picker)

Endpoints powering the frontend **Asset Library Picker (Teacher Flow 2)**:
- `GET /api/video-uploading?status=ready` (List ready tenant videos)
- `GET /api/documents` (List tenant documents, already built in Phase 2)
- `GET /api/assessments` (List tenant quizzes)

---

## Verification Plan

1. **Unit Tests (`CoursePoolTests.cs`):**
   - Video added to pool -> exists in `course.ResourcePool`.
   - Video assigned from pool -> removed from pool, exists in section items.
   - Lesson unassigned -> removed from section items, returned to pool.
   - Course publish attempt with items in pool -> rejected with `Course.ResourcePoolNotEmpty`.
   - Dismiss from pool -> removed from pool, course can publish.
2. **Integration Tests (`CourseTests.cs`):**
   - Update integration tests to use the new endpoints.
   - Verify `AddVideoToCoursePoolConsumer` populates the pool upon `VideoPublishedEvent`.
3. **Build Verification:**
   - Full solution build with zero errors.
