# Implementation Plan: CourseAsset as Source of Truth

**Platform:** AlphaZero Learning Academy  
**Status:** Phase 1 & Phase 2 Complete. Phase 3 Revised.  
**Revision:** 4.0 — CourseAsset replaces CourseResourcePoolItem and ARN-based CurriculumResource

---

## Completed Phases

- [x] **Phase 1: Foundation Fixes** — Bitmask decoupling, `ActiveTrackedItems`, exhaustive `GetExpectedServiceForMainType`. All 27 tests passing.
- [x] **Phase 2: Documents Module** — Clean Architecture scaffold, presigned S3 upload/download, metadata query. All 9 tests passing.

---

## Phase 3: CourseAsset as Single Source of Truth

### Design Principles

1. **CourseAsset IS the pool.** No separate `CourseResourcePoolItem`. An asset with `State = Available` is in the pool. `State = InUse` means assigned to curriculum.
2. **CurriculumResource points to CourseAsset** (FK), not to an ARN string. No more `Primary`/`Auxiliary` type distinction.
3. **Consumers are thin.** Infrastructure consumers receive events and delegate to Application layer MediatR commands. Zero business logic in Infrastructure.
4. **Hybrid Architecture for Scalability & Integrity.** In EF Core, `Course` maintains a 1-to-many relationship to `_assets`. Background ingestion and readiness events operate directly via `IRepository<CourseAsset>` for high-concurrency, lock-free $O(1)$ writes. Curriculum authoring (`AssignAssetToCurriculum`, `UnassignFromCurriculum`) operates through the `Course` aggregate to strictly enforce syllabus invariants and atomic bitmask indexing.
5. **IArnResolver enables cross-module queries.** Video module asks Courses "does asset X exist in course Y?" via DI interface, not direct DB access.

---

### 3.1 Domain: Fix CourseAsset Hierarchy

**File:** [`Modules/Courses/Domain/Aggregates/Courses/CourseMediaAsset.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Domain/Aggregates/Courses/CourseMediaAsset.cs)

**Changes:**
- Change `CourseAsset` constructor from `private` to `protected`
- Add parameterless `protected` constructor for EF Core
- Add state transition methods (`MarkAvailable`, `MarkFailed`, `MarkInUse`, `ReturnToPool`, `Archive`)
- Implement real `Create()` factories on all subtypes
- Add domain validation in state transitions

```csharp
public abstract class CourseAsset : TenantOwnedEntity
{
    // EF Core constructor
    protected CourseAsset() : base(default, default) { }

    protected CourseAsset(
        Guid id, Guid tenantId, ResourceArn resourceArn, 
        string title, Guid courseId, DateTime uploadedUtcAt, CourseAssetState state)
        : base(id, tenantId)
    {
        ResourceArn = resourceArn;
        Title = title;
        CourseId = courseId;
        UploadedUtcAt = uploadedUtcAt;
        State = state;
    }

    public abstract CourseAssetType CourseAssetType { get; }
    public ResourceArn ResourceArn { get; private set; }
    public string Title { get; private set; }
    public Guid CourseId { get; private set; }
    public CourseAssetState State { get; private set; }
    public DateTime UploadedUtcAt { get; private set; }

    // ─── State Transitions ───

    public ErrorOr<Success> MarkAvailable()
    {
        if (State != CourseAssetState.Pending)
            return Error.Conflict("CourseAsset.State", 
                $"Cannot mark asset as available from state '{State}'. Must be Pending.");
        State = CourseAssetState.Available;
        return Result.Success;
    }

    public ErrorOr<Success> MarkFailed()
    {
        if (State != CourseAssetState.Pending)
            return Error.Conflict("CourseAsset.State", 
                $"Cannot mark asset as failed from state '{State}'. Must be Pending.");
        State = CourseAssetState.Failed;
        return Result.Success;
    }

    public ErrorOr<Success> MarkInUse()
    {
        if (State != CourseAssetState.Available)
            return Error.Conflict("CourseAsset.State", 
                $"Cannot mark asset as in-use from state '{State}'. Must be Available.");
        State = CourseAssetState.InUse;
        return Result.Success;
    }

    public ErrorOr<Success> ReturnToPool()
    {
        if (State != CourseAssetState.InUse)
            return Error.Conflict("CourseAsset.State", 
                $"Cannot return asset to pool from state '{State}'. Must be InUse.");
        State = CourseAssetState.Available;
        return Result.Success;
    }

    public ErrorOr<Success> Archive()
    {
        if (State == CourseAssetState.InUse)
            return Error.Conflict("CourseAsset.State", 
                "Cannot archive an asset that is currently in use. Unassign it first.");
        State = CourseAssetState.Archived;
        return Result.Success;
    }

    public void UpdateTitle(string title)
    {
        if (!string.IsNullOrWhiteSpace(title))
            Title = title;
    }
}

// ─── Enums (unchanged) ───

public enum CourseAssetState { Pending, Available, Archived, InUse, Failed }
public enum CourseAssetType { Video, Document, Assessment }

// ─── Subtypes ───

public class VideoCourseAsset : CourseAsset
{
    public override CourseAssetType CourseAssetType => CourseAssetType.Video;
    public TimeSpan Duration { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public string? RelativeStreamingUrl { get; private set; }

    private VideoCourseAsset() { } // EF Core

    private VideoCourseAsset(
        Guid id, Guid tenantId, ResourceArn arn, string title, 
        Guid courseId, DateTime uploadedAt)
        : base(id, tenantId, arn, title, courseId, uploadedAt, CourseAssetState.Pending) { }

    public static ErrorOr<VideoCourseAsset> Create(
        Guid id, Guid tenantId, ResourceArn videoArn, string title, Guid courseId)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("VideoCourseAsset.Title", "Title is required.");

        return new VideoCourseAsset(id, tenantId, videoArn, title, courseId, DateTime.UtcNow);
    }

    public void UpdateStreamingInfo(TimeSpan duration, string? relativeUrl, string? thumbnailUrl)
    {
        Duration = duration;
        RelativeStreamingUrl = relativeUrl;
        ThumbnailUrl = thumbnailUrl;
    }
}

public class DocumentCourseAsset : CourseAsset
{
    public override CourseAssetType CourseAssetType => CourseAssetType.Document;
    public string FileName { get; private set; } = null!;
    public long Size { get; private set; }
    public string ContentType { get; private set; } = null!;

    private DocumentCourseAsset() { }

    private DocumentCourseAsset(
        Guid id, Guid tenantId, ResourceArn arn, string title,
        Guid courseId, string fileName, long size, string contentType, DateTime uploadedAt)
        : base(id, tenantId, arn, title, courseId, uploadedAt, CourseAssetState.Pending)
    {
        FileName = fileName;
        Size = size;
        ContentType = contentType;
    }

    public static ErrorOr<DocumentCourseAsset> Create(
        Guid id, Guid tenantId, ResourceArn docArn, string title,
        Guid courseId, string fileName, long size = 0, string contentType = "")
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("DocumentCourseAsset.Title", "Title is required.");
        if (string.IsNullOrWhiteSpace(fileName))
            return Error.Validation("DocumentCourseAsset.FileName", "File name is required.");

        // Documents start as Pending (reserves space before file upload is completed)
        return new DocumentCourseAsset(id, tenantId, docArn, title, courseId, fileName, size, contentType, DateTime.UtcNow);
    }

    public void UpdateFileInfo(string fileName, long size, string contentType)
    {
        FileName = fileName;
        Size = size;
        ContentType = contentType;
    }
}

public class AssessmentCourseAsset : CourseAsset
{
    public override CourseAssetType CourseAssetType => CourseAssetType.Assessment;
    public int QuestionsNumber { get; private set; }
    public AssessmentType Type { get; private set; }

    private AssessmentCourseAsset() { }

    private AssessmentCourseAsset(
        Guid id, Guid tenantId, ResourceArn arn, string title,
        Guid courseId, int questionsNumber, AssessmentType type, DateTime uploadedAt)
        : base(id, tenantId, arn, title, courseId, uploadedAt, CourseAssetState.Pending)
    {
        QuestionsNumber = questionsNumber;
        Type = type;
    }

    public static ErrorOr<AssessmentCourseAsset> Create(
        Guid id, Guid tenantId, ResourceArn assessmentArn, string title,
        Guid courseId, int questionsNumber = 0, AssessmentType type = AssessmentType.Practice)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("AssessmentCourseAsset.Title", "Title is required.");

        // Assessments start as Pending (reserves space before questions/draft are completed)
        return new AssessmentCourseAsset(id, tenantId, assessmentArn, title, courseId, questionsNumber, type, DateTime.UtcNow);
    }

    public void UpdateAssessmentInfo(int questionsNumber, AssessmentType type)
    {
        QuestionsNumber = questionsNumber;
        Type = type;
    }
}

public enum AssessmentType { Practice, Midterm, Final }
```

**State machine:**
```
                    ┌──────────┐
        Create() ──►│ Pending  │──► MarkFailed() ──► Failed
                    └────┬─────┘
                         │ MarkAvailable()
                         ▼
                    ┌──────────┐
        ┌──────────│ Available │◄──── ReturnToPool()
        │          └────┬─────┘           ▲
        │               │ MarkInUse()     │
        │               ▼                 │
        │          ┌──────────┐           │
        │          │  InUse   │───────────┘
        │          └──────────┘
        │ Archive()
        ▼
   ┌──────────┐
   │ Archived │
   └──────────┘
```

> **Note:** ALL asset subtypes (`VideoCourseAsset`, `DocumentCourseAsset`, `AssessmentCourseAsset`) start in `CourseAssetState.Pending` state to reserve their space/slot before the file upload or authoring is confirmed. When ready, each transitions to `Available`.

---

### 3.2 Domain: Update CurriculumResource

**File:** [`Modules/Courses/Domain/Aggregates/Courses/CurriculumResource.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Domain/Aggregates/Courses/CurriculumResource.cs)

**Changes:**
- Remove `Arn` and `Type` properties (replaced by CourseAsset FK)
- Add `CourseAssetId` FK property
- Keep `Asset` navigation, `Order`, `Metadata`
- Update constructor

```csharp
namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class CurriculumResource
{
    public Guid CourseAssetId { get; private set; }
    public CourseAsset Asset { get; private set; }
    public int Order { get; internal set; }
    public JsonElement Metadata { get; private set; }

    private CurriculumResource() { } // EF Core

    public CurriculumResource(CourseAsset asset, int order, JsonElement metadata)
    {
        CourseAssetId = asset.Id;
        Asset = asset;
        Order = order;
        Metadata = metadata;
    }

    internal ErrorOr<Success> UpdateOrder(int order)
    {
        if (order < 0)
            return Error.Validation("Courses.Resources.Order.Validation", "Order cannot be negative.");
        Order = order;
        return Result.Success;
    }

    internal void UpdateMetadata(JsonElement metadata)
    {
        Metadata = metadata;
    }
}
```

---

### 3.3 Domain: Update CurriculumItem

**File:** [`Modules/Courses/Domain/Aggregates/Courses/CurriculumItem.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Domain/Aggregates/Courses/CurriculumItem.cs)

**Changes:**
- Replace `AddResource(ResourceArn, string type, JsonElement)` with `AddResource(CourseAsset, JsonElement)`
- Update type validation to use `CourseAssetType` enum instead of ARN service prefix
- Remove `ReorderResources(List<ResourceArn>)` — replace with `ReorderResources(List<Guid> assetIds)`
- Keep `MainType` property — derived from first asset's `CourseAssetType`

```csharp
public ErrorOr<Success> AddResource(CourseAsset asset, JsonElement metadata)
{
    // BOLA check: validate resource tenant matches item tenant
    if (asset.TenantId != TenantId)
        return Error.Validation("CurriculumItem.TenantMismatch", "Asset tenant must match item tenant.");

    // First resource validates type compatibility with MainType
    if (_resources.Count == 0)
    {
        if (!IsCompatibleType(MainType, asset.CourseAssetType))
            return Error.Validation("CurriculumItem.TypeMismatch", 
                $"Asset type '{asset.CourseAssetType}' does not match item MainType '{MainType}'.");
    }

    var order = _resources.Count;
    var resource = new CurriculumResource(asset, order, metadata);
    _resources.Add(resource);
    return Result.Success;
}

public void ReorderResources(List<Guid> orderedAssetIds)
{
    var temp = _resources.ToList();
    _resources.Clear();

    for (int i = 0; i < orderedAssetIds.Count; i++)
    {
        var res = temp.FirstOrDefault(r => r.CourseAssetId == orderedAssetIds[i]);
        if (res != null)
        {
            res.UpdateOrder(i);
            _resources.Add(res);
        }
    }

    foreach (var res in temp.Where(r => !_resources.Contains(r)))
    {
        res.UpdateOrder(_resources.Count);
        _resources.Add(res);
    }
}

private static bool IsCompatibleType(string mainType, CourseAssetType assetType)
{
    return mainType.ToLowerInvariant() switch
    {
        "video" => assetType == CourseAssetType.Video,
        "quiz" or "assessment" => assetType == CourseAssetType.Assessment,
        "document" => assetType == CourseAssetType.Document,
        _ => false
    };
}
```

**Delete:** `GetExpectedServiceForMainType` method (replaced by `IsCompatibleType`).

---

### 3.4 Domain: Update Course.cs

**File:** [`Modules/Courses/Domain/Aggregates/Courses/Course.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Domain/Aggregates/Courses/Course.cs)

**Changes:**
- Add `_assets` collection (CourseAsset navigation property)
- Add asset creation methods (`AddVideoAsset`, `AddDocumentAsset`, `AddAssessmentAsset`)
- Add asset state transition methods (`MarkAssetAvailable`, `MarkAssetFailed`)
- Add pool operations (`AssignAssetToCurriculum`, `UnassignFromCurriculum`, `DismissAsset`)
- Update `AddCurriculumItem` to accept `CourseAsset` instead of `ResourceArn`
- Move publishing invariant into `Course.Publish()` (self-contained — no external query needed)
- Remove `UpdateResourceMetadata`, `LinkResourceToItem` (ARN-based — replaced by asset methods)

```csharp
// ─── New: Asset Collection ───
private readonly List<CourseAsset> _assets = new();
public IReadOnlyCollection<CourseAsset> Assets => _assets.AsReadOnly();

// ─── Derived Pool View ───
public IEnumerable<CourseAsset> PoolAssets => _assets.Where(a => a.State == CourseAssetState.Available);
public IEnumerable<CourseAsset> PendingAssets => _assets.Where(a => a.State == CourseAssetState.Pending);

// ─── 1. Asset Creation (called by consumer MediatR handlers) ───

public ErrorOr<VideoCourseAsset> AddVideoAsset(Guid videoId, ResourceArn videoArn, string title)
{
    // Idempotency
    if (_assets.Any(a => a.Id == videoId))
        return (VideoCourseAsset)_assets.First(a => a.Id == videoId);

    var result = VideoCourseAsset.Create(videoId, TenantId, videoArn, title, Id);
    if (result.IsError) return result.Errors;

    _assets.Add(result.Value);
    return result.Value;
}

public ErrorOr<DocumentCourseAsset> AddDocumentAsset(
    Guid docId, ResourceArn docArn, string title, string fileName, long size, string contentType)
{
    if (_assets.Any(a => a.Id == docId))
        return (DocumentCourseAsset)_assets.First(a => a.Id == docId);

    var result = DocumentCourseAsset.Create(docId, TenantId, docArn, title, Id, fileName, size, contentType);
    if (result.IsError) return result.Errors;

    _assets.Add(result.Value);
    return result.Value;
}

public ErrorOr<AssessmentCourseAsset> AddAssessmentAsset(
    Guid assessmentId, ResourceArn assessmentArn, string title, int questionsNumber, AssessmentType type)
{
    if (_assets.Any(a => a.Id == assessmentId))
        return (AssessmentCourseAsset)_assets.First(a => a.Id == assessmentId);

    var result = AssessmentCourseAsset.Create(assessmentId, TenantId, assessmentArn, title, Id, questionsNumber, type);
    if (result.IsError) return result.Errors;

    _assets.Add(result.Value);
    return result.Value;
}

// ─── 2. Asset State Transitions (called by consumer MediatR handlers) ───

public ErrorOr<Success> MarkAssetAvailable(Guid assetId, TimeSpan? duration = null, string? relativeUrl = null, string? thumbnailUrl = null)
{
    var asset = _assets.FirstOrDefault(a => a.Id == assetId);
    if (asset is null)
        return Error.NotFound("CourseAsset.NotFound", "Asset not found in this course.");

    if (asset is VideoCourseAsset video && (duration.HasValue || relativeUrl is not null))
        video.UpdateStreamingInfo(duration ?? TimeSpan.Zero, relativeUrl, thumbnailUrl);

    return asset.MarkAvailable();
}

public ErrorOr<Success> MarkAssetFailed(Guid assetId)
{
    var asset = _assets.FirstOrDefault(a => a.Id == assetId);
    if (asset is null)
        return Error.NotFound("CourseAsset.NotFound", "Asset not found in this course.");

    return asset.MarkFailed();
}

// ─── 3. Pool Operations (teacher-facing) ───

/// <summary>
/// Teacher assigns an Available asset from the pool to a section, creating a CurriculumItem.
/// </summary>
public ErrorOr<CurriculumItem> AssignAssetToCurriculum(Guid assetId, Guid sectionId, string title, JsonElement metadata)
{
    var asset = _assets.FirstOrDefault(a => a.Id == assetId);
    if (asset is null)
        return Error.NotFound("CourseAsset.NotFound", "Asset not found in this course.");
    if (asset.State != CourseAssetState.Available)
        return Error.Conflict("CourseAsset.NotAvailable",
            $"Asset is in state '{asset.State}', must be Available to assign.");

    var section = _sections.FirstOrDefault(s => s.Id == sectionId);
    if (section is null)
        return Error.NotFound("Course.Section", "Section not found.");

    // Create the curriculum item
    var mainType = asset.CourseAssetType.ToString();
    var item = new CurriculumItem(
        Guid.NewGuid(), TenantId, sectionId, title,
        section.Items.Count, NextAvailableBitIndex++, mainType);

    var addResult = item.AddResource(asset, metadata);
    if (addResult.IsError) return addResult.Errors;

    section.AddItem(item);

    // Transition asset state atomically
    var stateResult = asset.MarkInUse();
    if (stateResult.IsError) return stateResult.Errors;

    return item;
}

/// <summary>
/// Removes an item from curriculum and returns all its assets to the pool.
/// </summary>
public ErrorOr<Success> UnassignFromCurriculum(Guid itemId)
{
    var section = _sections.FirstOrDefault(s => s.Items.Any(i => i.Id == itemId));
    if (section is null)
        return Error.NotFound("Section.NotFound", "No section contains this item.");

    var item = section.Items.FirstOrDefault(i => i.Id == itemId);
    if (item is null)
        return Error.NotFound("CurriculumItem.NotFound");

    // Return ALL assets associated with this curriculum item back to the pool
    foreach (var resource in item.Resources)
    {
        var asset = _assets.FirstOrDefault(a => a.Id == resource.CourseAssetId);
        if (asset is not null && asset.State == CourseAssetState.InUse)
        {
            asset.ReturnToPool(); // InUse → Available
        }
    }

    // Soft-delete item (bitmask index preserved)
    item.Delete();
    return Result.Success;
}

/// <summary>
/// Dismisses an unused asset from the pool (retains in tenant library).
/// </summary>
public ErrorOr<Success> DismissAsset(Guid assetId)
{
    var asset = _assets.FirstOrDefault(a => a.Id == assetId);
    if (asset is null)
        return Error.NotFound("CourseAsset.NotFound");

    return asset.Archive();
}

// ─── 4. Updated Publish (Pool items allowed) ───

public ErrorOr<Success> Publish()
{
    if (Status != CourseStatus.Approved)
        return Error.Conflict("Course.Status", "Only approved courses can be published.");

    if (_plans.Count == 0)
        return Error.Validation("Course.NoPlans", "Course must have at least one plan before it can be published.");

    // Note: Staged/draft assets are allowed to remain in the course pool during publishing
    // (e.g. for upcoming sections or future updates).

    Status = CourseStatus.Published;
    AddDomainEvent(new CoursePublishedDomainEvent(Id));
    return Result.Success;
}
```

**Remove entirely:**
```csharp
// DELETE — replaced by AssignAssetToCurriculum
public ErrorOr<Success> AddCurriculumItem(Guid sectionId, string title, string mainType, ResourceArn primaryResourceArn, JsonElement metadata) { ... }

// DELETE — replaced by asset methods
public ErrorOr<Success> LinkResourceToItem(Guid itemId, ResourceArn resourceArn, string type, JsonElement metadata) { ... }

// DELETE — metadata now on CourseAsset subtypes
public void UpdateResourceMetadata(Guid resourceId, JsonElement metadata) { ... }
```


---

### 3.5 Infrastructure: EF Core CourseAsset Configuration (TPH)

**Mapping Strategy: Table Per Hierarchy (TPH).** All course assets are stored in a single table `Courses.CourseAssets` with an `AssetType` discriminator column (`Video`, `Document`, `Assessment`).

**New file:** `Modules/Courses/Infrastructure/Persistance/Configurations/CourseAssetConfiguration.cs`

```csharp
public class CourseAssetConfiguration : IEntityTypeConfiguration<CourseAsset>
{
    public void Configure(EntityTypeBuilder<CourseAsset> builder)
    {
        builder.ToTable("CourseAssets", "Courses");

        // TPH discriminator
        builder.HasDiscriminator<string>("AssetType")
            .HasValue<VideoCourseAsset>("Video")
            .HasValue<DocumentCourseAsset>("Document")
            .HasValue<AssessmentCourseAsset>("Assessment");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.ResourceArn)
            .HasConversion(
                arn => arn.Value,
                val => ResourceArn.Create(val).Value)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Title).IsRequired().HasMaxLength(256);
        builder.Property(a => a.CourseId).IsRequired();
        builder.Property(a => a.State).IsRequired()
            .HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.UploadedUtcAt).IsRequired();

        builder.HasIndex(a => new { a.CourseId, a.State });
        builder.HasIndex(a => a.ResourceArn).IsUnique();

        // Relationship: Course owns many CourseAssets
        builder.HasOne<Course>()
            .WithMany(c => c.Assets)
            .HasForeignKey(a => a.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VideoCourseAssetConfiguration : IEntityTypeConfiguration<VideoCourseAsset>
{
    public void Configure(EntityTypeBuilder<VideoCourseAsset> builder)
    {
        builder.Property(v => v.Duration);
        builder.Property(v => v.ThumbnailUrl).HasMaxLength(500);
        builder.Property(v => v.RelativeStreamingUrl).HasMaxLength(500);
    }
}

public class DocumentCourseAssetConfiguration : IEntityTypeConfiguration<DocumentCourseAsset>
{
    public void Configure(EntityTypeBuilder<DocumentCourseAsset> builder)
    {
        builder.Property(d => d.FileName).HasMaxLength(256);
        builder.Property(d => d.Size);
        builder.Property(d => d.ContentType).HasMaxLength(100);
    }
}

public class AssessmentCourseAssetConfiguration : IEntityTypeConfiguration<AssessmentCourseAsset>
{
    public void Configure(EntityTypeBuilder<AssessmentCourseAsset> builder)
    {
        builder.Property(a => a.QuestionsNumber);
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
    }
}
```

---

### 3.6 Infrastructure: Update CurriculumResource EF Configuration

**File:** [`Modules/Courses/Infrastructure/Persistance/Configurations/CurriculumItemConfiguration.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Infrastructure/Persistance/Configurations/CurriculumItemConfiguration.cs)

**Changes:** Replace `Arn` + `Type` column mapping with `CourseAssetId` FK.

```csharp
builder.OwnsMany(i => i.Resources, rb =>
{
    rb.ToTable("CurriculumResources", "Courses");
    rb.WithOwner().HasForeignKey("CurriculumItemId");
    rb.Property<Guid>("Id").ValueGeneratedOnAdd();
    rb.HasKey("Id");

    // NEW: FK to CourseAsset (replaces Arn + Type columns)
    rb.Property(r => r.CourseAssetId).IsRequired();
    rb.HasOne(r => r.Asset)
        .WithMany()
        .HasForeignKey(r => r.CourseAssetId)
        .OnDelete(DeleteBehavior.Restrict);

    rb.Property(r => r.Order).IsRequired();
    rb.Property(r => r.Metadata).HasColumnType("jsonb");

    // DELETE these lines:
    // rb.Property(r => r.Arn)...
    // rb.Property(r => r.Type)...
});
```

---

### 3.7 Infrastructure: Update AppDbContext

**File:** [`Modules/Courses/Infrastructure/Persistance/AppDbContext.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Infrastructure/Persistance/AppDbContext.cs)

**Add:**
```csharp
public DbSet<CourseAsset> CourseAssets => Set<CourseAsset>();
```

---

### 3.8 Infrastructure: Repository Architecture (Hybrid Approach)

We apply the **Hybrid Approach**:
1. **Curriculum Authoring (`Assign`, `Unassign`)**: Operates through `ICourseRepository` with eager-loaded joins so that `Course`, `Sections`, `Items`, `Resources`, and `Assets` are updated consistently within the aggregate boundary.
2. **Asynchronous Ingestion & Readiness (`Create`, `MarkAvailable`, `MarkFailed`, `Dismiss`)**: Operates directly on `CourseAsset` via the generic `IRepository<CourseAsset>` from `AlphaZero.Shared.Infrastructure.Repositores` for high-throughput, lock-free $O(1)$ writes.

#### 3.8.1 ICourseRepository — Joined Curriculum Method

**File:** `Modules/Courses/Application/Repositories/ICourseRepository.cs`

**Add to `ICourseRepository`:**
```csharp
// Load Course with sections, items, resources, AND assets eagerly joined (for assign/unassign)
Task<Course?> GetByIdWithSectionsAndAssetsAsync(Guid courseId, CancellationToken ct = default);
```

**Implementation in `CourseRepository.cs`:**
```csharp
public async Task<Course?> GetByIdWithSectionsAndAssetsAsync(Guid courseId, CancellationToken ct = default)
    => await _context.Courses
        .Include(c => c.Sections)
            .ThenInclude(s => s.Items)
                .ThenInclude(i => i.Resources)
        .Include(c => c.Assets)
        .FirstOrDefaultAsync(c => c.Id == courseId, ct);
```

#### 3.8.2 Generic IRepository<CourseAsset> from Shared

Registered in DI:
```csharp
moduleServices.AddScoped<IRepository<CourseAsset>, BaseRepository<AppDbContext, CourseAsset>>();
```
Injected into ingestion commands, readiness commands, dismissal, and `CourseArnResolver`.

---

### 3.9 Application: CourseAsset Commands

All commands go in `Modules/Courses/Application/Courses/Commands/Assets/`.

#### 3.9.1 CreateVideoCourseAssetCommand

**New file:** `Commands/Assets/CreateVideoCourseAsset.cs`

Called by `VideoUploadingStartedEventHandler` when a video upload begins.

#### 3.9.1 CreateVideoCourseAssetCommand (Direct Ingestion)

**New file:** `Commands/Assets/CreateVideoCourseAsset.cs`

Called by `VideoUploadingStartedEventHandler` when a video upload begins. Uses `IRepository<CourseAsset>` directly for lock-free $O(1)$ ingestion.

```csharp
public record CreateVideoCourseAssetCommand(
    Guid VideoId,        // becomes the CourseAsset.Id
    Guid TenantId,
    Guid CourseId,       // extracted from TargetResourceArn
    string Title
) : ICommand<Success>;

public class CreateVideoCourseAssetCommandValidator : AbstractValidator<CreateVideoCourseAssetCommand>
{
    public CreateVideoCourseAssetCommandValidator()
    {
        RuleFor(x => x.VideoId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
    }
}

public sealed class CreateVideoCourseAssetCommandHandler 
    : IRequestHandler<CreateVideoCourseAssetCommand, ErrorOr<Success>>
{
    private readonly IRepository<CourseAsset> _assetRepository;

    public CreateVideoCourseAssetCommandHandler(IRepository<CourseAsset> assetRepository)
        => _assetRepository = assetRepository;

    public async Task<ErrorOr<Success>> Handle(
        CreateVideoCourseAssetCommand request, CancellationToken ct)
    {
        // Idempotency: check if asset already exists
        var existing = await _assetRepository.GetById(request.VideoId, ct);
        if (existing is not null)
            return Result.Success;

        var videoArn = ResourceArn.ForVideo(request.TenantId, request.VideoId);
        var assetResult = VideoCourseAsset.Create(
            request.VideoId, request.TenantId, videoArn, request.Title, request.CourseId);

        if (assetResult.IsError) return assetResult.Errors;

        _assetRepository.Add(assetResult.Value);
        return Result.Success;
        // UnitOfWork saves via pipeline behavior
    }
}
```

#### 3.9.2 Asset Readiness Strategy Pattern & MarkCourseAssetAvailableCommand

To keep the application layer open for future asset types (e.g. SCORM, Audio, LiveStream) without rewriting handlers, we use the **Strategy Pattern** to apply type-specific readiness payloads.

**File:** `Modules/Courses/Application/Courses/Commands/Assets/ICourseAssetReadinessStrategy.cs`

```csharp
public interface ICourseAssetReadinessStrategy
{
    CourseAssetType AssetType { get; }
    ErrorOr<Success> ApplyReadiness(CourseAsset asset, JsonElement? payload);
}
```

**Strategies:**

```csharp
public class VideoAssetReadinessStrategy : ICourseAssetReadinessStrategy
{
    public CourseAssetType AssetType => CourseAssetType.Video;

    public ErrorOr<Success> ApplyReadiness(CourseAsset asset, JsonElement? payload)
    {
        if (asset is not VideoCourseAsset video)
            return Error.Validation("Strategy.TypeMismatch", "Asset is not a VideoCourseAsset.");

        if (payload.HasValue && payload.Value.ValueKind == JsonValueKind.Object)
        {
            var el = payload.Value;
            var relativeUrl = el.TryGetProperty("relativeUrl", out var u) ? u.GetString() : null;
            var thumbnailUrl = el.TryGetProperty("thumbnailUrl", out var t) ? t.GetString() : null;
            TimeSpan duration = TimeSpan.Zero;
            if (el.TryGetProperty("duration", out var d) && d.GetString() is string durStr && TimeSpan.TryParse(durStr, out var parsedDur))
            {
                duration = parsedDur;
            }

            video.UpdateStreamingInfo(duration, relativeUrl, thumbnailUrl);
        }

        return Result.Success;
    }
}

public class DocumentAssetReadinessStrategy : ICourseAssetReadinessStrategy
{
    public CourseAssetType AssetType => CourseAssetType.Document;

    public ErrorOr<Success> ApplyReadiness(CourseAsset asset, JsonElement? payload)
    {
        if (asset is not DocumentCourseAsset doc)
            return Error.Validation("Strategy.TypeMismatch", "Asset is not a DocumentCourseAsset.");

        if (payload.HasValue && payload.Value.ValueKind == JsonValueKind.Object)
        {
            var el = payload.Value;
            var fileName = el.TryGetProperty("fileName", out var f) ? f.GetString() ?? doc.FileName : doc.FileName;
            var size = el.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz) ? sz : doc.Size;
            var contentType = el.TryGetProperty("contentType", out var c) ? c.GetString() ?? doc.ContentType : doc.ContentType;

            doc.UpdateFileInfo(fileName, size, contentType);
        }

        return Result.Success;
    }
}

public class AssessmentAssetReadinessStrategy : ICourseAssetReadinessStrategy
{
    public CourseAssetType AssetType => CourseAssetType.Assessment;

    public ErrorOr<Success> ApplyReadiness(CourseAsset asset, JsonElement? payload)
    {
        if (asset is not AssessmentCourseAsset assessment)
            return Error.Validation("Strategy.TypeMismatch", "Asset is not an AssessmentCourseAsset.");

        if (payload.HasValue && payload.Value.ValueKind == JsonValueKind.Object)
        {
            var el = payload.Value;
            var questionsNumber = el.TryGetProperty("questionsNumber", out var q) && q.TryGetInt32(out var qn) ? qn : assessment.QuestionsNumber;
            var type = assessment.Type;
            if (el.TryGetProperty("type", out var t) && Enum.TryParse<AssessmentType>(t.GetString(), out var parsedType))
            {
                type = parsedType;
            }

            assessment.UpdateAssessmentInfo(questionsNumber, type);
        }

        return Result.Success;
    }
}
```

**Unified Command & Handler:** `Commands/Assets/MarkCourseAssetAvailable.cs`

```csharp
public record MarkCourseAssetAvailableCommand(
    Guid AssetId,
    JsonElement? Payload = null
) : ICommand<Success>;

public sealed class MarkCourseAssetAvailableCommandHandler 
    : IRequestHandler<MarkCourseAssetAvailableCommand, ErrorOr<Success>>
{
    private readonly IRepository<CourseAsset> _assetRepository;
    private readonly IEnumerable<ICourseAssetReadinessStrategy> _strategies;

    public MarkCourseAssetAvailableCommandHandler(
        IRepository<CourseAsset> assetRepository,
        IEnumerable<ICourseAssetReadinessStrategy> strategies)
    {
        _assetRepository = assetRepository;
        _strategies = strategies;
    }

    public async Task<ErrorOr<Success>> Handle(
        MarkCourseAssetAvailableCommand request, CancellationToken ct)
    {
        var asset = await _assetRepository.GetById(request.AssetId, ct);
        if (asset is null)
            return Error.NotFound("CourseAsset.NotFound", "Course asset not found.");

        // Apply type-specific strategy
        var strategy = _strategies.FirstOrDefault(s => s.AssetType == asset.CourseAssetType);
        if (strategy is not null)
        {
            var strategyResult = strategy.ApplyReadiness(asset, request.Payload);
            if (strategyResult.IsError) return strategyResult.Errors;
        }

        var markResult = asset.MarkAvailable();
        if (markResult.IsError) return markResult.Errors;

        _assetRepository.Update(asset);
        return Result.Success;
    }
}
```

#### 3.9.3 MarkCourseAssetFailedCommand

**New file:** `Commands/Assets/MarkCourseAssetFailed.cs`

```csharp
public record MarkCourseAssetFailedCommand(Guid AssetId, string Reason) : ICommand<Success>;

public sealed class MarkCourseAssetFailedCommandHandler 
    : IRequestHandler<MarkCourseAssetFailedCommand, ErrorOr<Success>>
{
    private readonly IRepository<CourseAsset> _assetRepository;
    private readonly ILogger<MarkCourseAssetFailedCommandHandler> _logger;

    public MarkCourseAssetFailedCommandHandler(
        IRepository<CourseAsset> assetRepository,
        ILogger<MarkCourseAssetFailedCommandHandler> logger)
    {
        _assetRepository = assetRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(
        MarkCourseAssetFailedCommand request, CancellationToken ct)
    {
        var asset = await _assetRepository.GetById(request.AssetId, ct);
        if (asset is null)
            return Error.NotFound("CourseAsset.NotFound", "Course asset not found.");

        var result = asset.MarkFailed();
        if (result.IsError) return result.Errors;

        _assetRepository.Update(asset);
        _logger.LogWarning("CourseAsset {AssetId} marked as failed. Reason: {Reason}", 
            request.AssetId, request.Reason);

        return Result.Success;
    }
}
```

#### 3.9.4 AssignAssetToCurriculumCommand (Via Course Aggregate)

**New file:** `Commands/Assets/AssignAssetToCurriculum.cs`

Teacher assigns an Available asset from the pool to a section. Operates through `Course` aggregate to maintain syllabus consistency.

```csharp
public record AssignAssetToCurriculumCommand(
    Guid CourseId,
    Guid SectionId,
    Guid AssetId,
    string Title,
    JsonElement? Metadata = null
) : ICommand<Guid>; // Returns new CurriculumItem.Id

public sealed class AssignAssetToCurriculumCommandHandler 
    : IRequestHandler<AssignAssetToCurriculumCommand, ErrorOr<Guid>>
{
    private readonly ICourseRepository _courseRepository;

    public AssignAssetToCurriculumCommandHandler(ICourseRepository courseRepository)
        => _courseRepository = courseRepository;

    public async Task<ErrorOr<Guid>> Handle(
        AssignAssetToCurriculumCommand request, CancellationToken ct)
    {
        // Eagerly joins sections, items, resources, and assets in one query
        var course = await _courseRepository.GetByIdWithSectionsAndAssetsAsync(request.CourseId, ct);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var metadata = request.Metadata ?? JsonDocument.Parse("{}").RootElement;
        var itemResult = course.AssignAssetToCurriculum(request.AssetId, request.SectionId, request.Title, metadata);
        if (itemResult.IsError) return itemResult.Errors;

        return itemResult.Value.Id;
    }
}
```

#### 3.9.5 UnassignAssetFromCurriculumCommand (Via Course Aggregate)

**New file:** `Commands/Assets/UnassignAssetFromCurriculum.cs`

Removes item from curriculum and returns **ALL** attached resources back to the pool. Operates through `Course` aggregate.

```csharp
public record UnassignAssetFromCurriculumCommand(
    Guid CourseId,
    Guid ItemId
) : ICommand<Success>;

public sealed class UnassignAssetFromCurriculumCommandHandler 
    : IRequestHandler<UnassignAssetFromCurriculumCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;

    public UnassignAssetFromCurriculumCommandHandler(ICourseRepository courseRepository)
        => _courseRepository = courseRepository;

    public async Task<ErrorOr<Success>> Handle(
        UnassignAssetFromCurriculumCommand request, CancellationToken ct)
    {
        // Eagerly joins sections, items, resources, and assets in one query
        var course = await _courseRepository.GetByIdWithSectionsAndAssetsAsync(request.CourseId, ct);
        if (course is null) return Error.NotFound("Course.NotFound");

        var result = course.UnassignFromCurriculum(request.ItemId);
        if (result.IsError) return result.Errors;

        return Result.Success;
    }
}
```

#### 3.9.6 DismissAssetCommand

**New file:** `Commands/Assets/DismissAsset.cs`

Removes unused asset from course pool (retains in tenant library).

```csharp
public record DismissAssetCommand(Guid CourseId, Guid AssetId) : ICommand<Success>;

public sealed class DismissAssetCommandHandler 
    : IRequestHandler<DismissAssetCommand, ErrorOr<Success>>
{
    private readonly IRepository<CourseAsset> _assetRepository;

    public DismissAssetCommandHandler(IRepository<CourseAsset> assetRepository)
        => _assetRepository = assetRepository;

    public async Task<ErrorOr<Success>> Handle(
        DismissAssetCommand request, CancellationToken ct)
    {
        var asset = await _assetRepository.GetById(request.AssetId, ct);
        if (asset is null) return Error.NotFound("CourseAsset.NotFound");
        if (asset.CourseId != request.CourseId)
            return Error.Forbidden("CourseAsset.WrongCourse", "Asset does not belong to this course.");

        var result = asset.Archive();
        if (result.IsError) return result.Errors;

        _assetRepository.Update(asset);
        return Result.Success;
    }
}
```

---

### 3.10 Application: PublishCourseCommandHandler (No Pool Check)

**File:** `Modules/Courses/Application/Courses/Commands/State/PublishCourse.cs`

Publishing does **not** block on remaining pool items. Educators can keep draft or upcoming staging items in the course pool while publishing the course.

```csharp
public sealed class PublishCourseCommandHandler : IRequestHandler<PublishCourseCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;

    public PublishCourseCommandHandler(ICourseRepository courseRepository)
        => _courseRepository = courseRepository;

    public async Task<ErrorOr<Success>> Handle(PublishCourseCommand request, CancellationToken ct)
    {
        var course = await _courseRepository.GetById(request.CourseId, ct);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        return course.Publish();
    }
}
```

---

### 3.11 Application: Queries

#### 3.11.1 GetCoursePoolQuery

**New file:** `Modules/Courses/Application/Courses/Queries/GetCoursePool.cs`

```csharp
public record GetCoursePoolQuery(Guid CourseId) : IQuery<List<CourseAssetDto>>;

public record CourseAssetDto(
    Guid Id,
    string Title,
    string AssetType,       // "Video", "Document", "Assessment"
    string State,           // "Pending", "Available", "Failed", "InUse", "Archived"
    string ResourceArn,
    DateTime UploadedAt,
    TimeSpan? Duration = null,
    string? ThumbnailUrl = null,
    string? FileName = null,
    int? QuestionsNumber = null
);

public sealed class GetCoursePoolQueryHandler 
    : IRequestHandler<GetCoursePoolQuery, ErrorOr<List<CourseAssetDto>>>
{
    private readonly IRepository<CourseAsset> _assetRepository;

    public GetCoursePoolQueryHandler(IRepository<CourseAsset> assetRepository)
        => _assetRepository = assetRepository;

    public async Task<ErrorOr<List<CourseAssetDto>>> Handle(
        GetCoursePoolQuery request, CancellationToken ct)
    {
        var assets = await _assetRepository.Get(
            a => a.CourseId == request.CourseId && a.State == CourseAssetState.Available, 
            ct);

        var poolAssets = assets
            .OrderBy(a => a.UploadedUtcAt)
            .Select(Map)
            .ToList();

        return poolAssets;
    }

    private static CourseAssetDto Map(CourseAsset asset)
    {
        var dto = new CourseAssetDto(
            asset.Id,
            asset.Title,
            asset.CourseAssetType.ToString(),
            asset.State.ToString(),
            asset.ResourceArn.Value,
            asset.UploadedUtcAt);

        return asset switch
        {
            VideoCourseAsset video => dto with
            {
                Duration = video.Duration,
                ThumbnailUrl = video.ThumbnailUrl
            },

            DocumentCourseAsset document => dto with
            {
                FileName = document.FileName
            },

            AssessmentCourseAsset assessment => dto with
            {
                QuestionsNumber = assessment.QuestionsNumber
            },

            _ => throw new ArgumentOutOfRangeException(nameof(asset))
        };
    }
}
```

---

### 3.12 Infrastructure: Consumer Implementations (Thin Wrappers)

**File:** [`Modules/Courses/Infrastructure/Consumers/Videos/VideoEventHandlers.cs`](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Infrastructure/Consumers/Videos/VideoEventHandlers.cs)

Thin wrappers delegating directly to MediatR commands. Decoupled and high-throughput.

```csharp
using AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Domain;
using MassTransit;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers.Videos;

/// <summary>
/// Video upload started → Create a Pending VideoCourseAsset in the course pool.
/// Only fires if TargetResourceArn contains a courseId (Flow 1: course-level upload).
/// </summary>
public class VideoUploadingStartedEventHandler : IConsumer<UploadVideoRequestedEvent>
{
    private readonly ICoursesModule _coursesModule;
    private readonly ILogger<VideoUploadingStartedEventHandler> _logger;

    public VideoUploadingStartedEventHandler(
        ICoursesModule coursesModule, 
        ILogger<VideoUploadingStartedEventHandler> logger)
    {
        _coursesModule = coursesModule;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UploadVideoRequestedEvent> context)
    {
        var msg = context.Message;

        if (string.IsNullOrEmpty(msg.TargetResourceArn))
            return;

        var arnResult = ResourceArn.Create(msg.TargetResourceArn);
        if (arnResult.IsError) return;

        var courseId = arnResult.Value.ExtractCourseId();
        if (courseId is null) return;

        var command = new CreateVideoCourseAssetCommand(
            msg.VideoId, msg.TenantId, courseId.Value, 
            $"Video {msg.VideoId:N}");

        var result = await _coursesModule.Send(command, context.CancellationToken);

        if (result.IsError)
            _logger.LogWarning("Failed to create VideoCourseAsset for Video {VideoId}: {Errors}", 
                msg.VideoId, string.Join(", ", result.Errors.Select(e => e.Description)));
        else
            _logger.LogInformation("Created Pending VideoCourseAsset for Video {VideoId} in Course {CourseId}.", 
                msg.VideoId, courseId.Value);
    }
}

/// <summary>
/// Video processing failed → Mark the CourseAsset as Failed.
/// </summary>
public class VideoUploadingFailedEventHandler : IConsumer<VideoProcessingFailedEvent>
{
    private readonly ICoursesModule _coursesModule;
    private readonly ILogger<VideoUploadingFailedEventHandler> _logger;

    public VideoUploadingFailedEventHandler(
        ICoursesModule coursesModule,
        ILogger<VideoUploadingFailedEventHandler> logger)
    {
        _coursesModule = coursesModule;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoProcessingFailedEvent> context)
    {
        var msg = context.Message;

        var command = new MarkCourseAssetFailedCommand(msg.VideoId, msg.Reason);
        var result = await _coursesModule.Send(command, context.CancellationToken);

        if (result.IsError)
            _logger.LogWarning("Failed to mark CourseAsset {VideoId} as failed: {Errors}", 
                msg.VideoId, string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}

/// <summary>
/// Video published (transcoding done) → Mark the CourseAsset as Available via strategy payload.
/// </summary>
public class VideoUploadedEventHandler : IConsumer<VideoPublishedEvent>
{
    private readonly ICoursesModule _coursesModule;
    private readonly ILogger<VideoUploadedEventHandler> _logger;

    public VideoUploadedEventHandler(
        ICoursesModule coursesModule,
        ILogger<VideoUploadedEventHandler> logger)
    {
        _coursesModule = coursesModule;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoPublishedEvent> context)
    {
        var msg = context.Message;

        var payload = JsonSerializer.SerializeToElement(new
        {
            relativeUrl = msg.RelativeUrl
        });

        var command = new MarkCourseAssetAvailableCommand(msg.VideoId, payload);
        var result = await _coursesModule.Send(command, context.CancellationToken);

        if (result.IsError)
            _logger.LogWarning("Failed to mark CourseAsset {VideoId} as available: {Errors}", 
                msg.VideoId, string.Join(", ", result.Errors.Select(e => e.Description)));
        else
            _logger.LogInformation("CourseAsset {VideoId} is now Available in pool.", msg.VideoId);
    }
}
```

---

### 3.13 Shared: IArnResolver Interface

**New file:** `AlphaZero.Shared/Authorization/IArnResolver.cs`

```csharp
namespace AlphaZero.Shared.Authorization;

/// <summary>
/// Cross-module interface for verifying asset existence within a container.
/// Registered by the owning module, consumed by external modules for permission checks.
/// </summary>
public interface IArnResolver
{
    Task<bool> IsResourceInContainerAsync(Guid containerId, Guid resourceId, CancellationToken ct = default);
}
```

**Courses module stub (user will implement):**
**New file:** `Modules/Courses/Infrastructure/Authorization/CourseArnResolver.cs`

```csharp
namespace AlphaZero.Modules.Courses.Infrastructure.Authorization;

public class CourseArnResolver : IArnResolver
{
    public Task<bool> IsResourceInContainerAsync(Guid containerId, Guid resourceId, CancellationToken ct = default)
    {
        // User will implement ARN resolution logic
        throw new NotImplementedException();
    }
}
```

---

### 3.14 Application: Delete Legacy Commands (Option A)

Delete legacy endpoints and commands entirely:
- **Delete:** `Modules/Courses/Application/Courses/Commands/AddLesson/AddLesson.cs` (`AddLessonCommand`)
- **Delete:** `Modules/Courses/Application/Courses/Commands/AddQuiz/AddQuiz.cs` (`AddAssessmentCommand`)
- **Delete:** `Modules/Courses/Presentation/Courses/AddItem/AddLesson.cs`
- **Delete:** `Modules/Courses/Presentation/Courses/AddItem/AddQuiz.cs`

All curriculum additions now go through `AssignAssetToCurriculumCommand`.

---

### 3.15 Presentation: Endpoints

**New file:** `Modules/Courses/Presentation/Courses/Pool/`

| Method | Route | Handler | Description |
|---|---|---|---|
| `GET` | `/courses/{courseId}/pool` | `GetCoursePoolQuery` | List assets in course pool (Available state) |
| `POST` | `/courses/{courseId}/pool/{assetId}/assign` | `AssignAssetToCurriculumCommand` | Assign pool asset to a section |
| `DELETE` | `/courses/{courseId}/pool/{assetId}/dismiss` | `DismissAssetCommand` | Remove from pool (keep in tenant library) |
| `DELETE` | `/courses/{courseId}/items/{itemId}/unassign` | `UnassignAssetFromCurriculumCommand` | Remove from curriculum, return all assets to pool |

---

### 3.16 DI Registration Summary

**In `DependencyInjection.cs` → `AddCoursesPrivateInfrastructure`:**
```csharp
// Standalone CourseAsset repository using BaseRepository from Shared
moduleServices.AddScoped<IRepository<CourseAsset>, BaseRepository<AppDbContext, CourseAsset>>();

// Asset Readiness Strategies
moduleServices.AddScoped<ICourseAssetReadinessStrategy, VideoAssetReadinessStrategy>();
moduleServices.AddScoped<ICourseAssetReadinessStrategy, DocumentAssetReadinessStrategy>();
moduleServices.AddScoped<ICourseAssetReadinessStrategy, AssessmentAssetReadinessStrategy>();
```

**In `DependencyInjection.cs` → `AddCoursesGlobalInfrastructure` (or module registration):**
```csharp
// IArnResolver implementation — available to other modules (e.g. Video module)
services.AddScoped<IArnResolver, CourseArnResolver>();
```

---

## Phase 4 & 5: Unchanged

Phase 4 (Contextual ACL & Cloudflare CDN) and Phase 5 (Tenant Asset Catalog) remain as specified in the [eng-review](file:///home/azero/Desktop/AlphaZeroLearningAcademy/plans/eng-review-course-architecture.md). The `IArnResolver` from 3.13 is the building block that Phase 4's ACL service will use.

---

## Verification Plan

### Unit Tests (`Courses.UnitTests`)

| Test Class | Tests |
|---|---|
| `CourseAssetTests` | Create each subtype validates inputs; state transitions enforce allowed paths; invalid transitions return errors |
| `CurriculumResourceTests` | Construct with CourseAsset sets FK and nav property; Order updates work; Metadata updates work |
| `CurriculumItemTests` | AddResource with matching type succeeds; AddResource with wrong type fails; tenant mismatch fails |
| `CourseAggregateTests` | AddCurriculumItem with asset creates item + resource; AddCurriculumItem increments BitIndex; item soft-delete works |
| `CourseAssetStateTests` | Full state machine: Pending→Available→InUse→Available→Archived; Pending→Failed; InUse cannot Archive |

### Integration Tests

| Test | Validates |
|---|---|
| `VideoUploadingStartedEventHandler_CreatesAsset` | Consumer creates Pending VideoCourseAsset via MediatR |
| `VideoUploadedEventHandler_MarksAvailable` | Consumer transitions asset to Available via readiness strategy |
| `VideoUploadingFailedEventHandler_MarksFailed` | Consumer transitions asset to Failed |
| `AssignAssetToCurriculum_HappyPath` | Asset moves from Available→InUse, CurriculumItem created |
| `UnassignAsset_ReturnsAllAssetsToPool` | All resources of unassigned item move from InUse→Available, item soft-deleted |
| `PublishCourse_AllowsPoolItems` | Publish succeeds even when assets remain in the course pool |
| `DismissAsset_Archival` | Asset archived, course can publish |

### Migration Test

| Test | Validates |
|---|---|
| `Migration_AddsColumnAndDropsOld` | `CurriculumResources` table gains `CourseAssetId` FK, drops `Arn` and `Type` columns |
| `CourseAssets_TableCreated` | `Courses.CourseAssets` TPH table exists with correct discriminator and indexes |

---

## Implementation Order (Dependency Graph)

```
Layer 1: Domain (no deps)
  ├── 3.1 Fix CourseAsset hierarchy (all start Pending)
  ├── 3.2 Update CurriculumResource (CourseAssetId FK)
  ├── 3.3 Update CurriculumItem (IsCompatibleType validation)
  └── 3.4 Update Course.cs (_assets collection, pool methods, unassign all, publish allows pool)

Layer 2: Infrastructure - Persistence (deps: Layer 1)
  ├── 3.5 EF Core CourseAsset config (TPH single table)
  ├── 3.6 Update CurriculumResource config (FK to CourseAssets)
  ├── 3.7 Update AppDbContext (DbSet<CourseAsset>)
  └── 3.8 ICourseRepository joined methods + Shared generic IRepository<CourseAsset>

Layer 3: Application (deps: Layers 1 + 2)
  ├── 3.9.1 CreateVideoCourseAssetCommand
  ├── 3.9.2 Strategy Pattern + MarkCourseAssetAvailableCommand
  ├── 3.9.3 MarkCourseAssetFailedCommand
  ├── 3.9.4 AssignAssetToCurriculumCommand
  ├── 3.9.5 UnassignAssetFromCurriculumCommand (returns ALL resources)
  ├── 3.9.6 DismissAssetCommand
  ├── 3.10 PublishCourseCommandHandler (no pool check)
  ├── 3.11 GetCoursePoolQuery (clean pattern-matching DTO mapper)
  └── 3.14 Delete legacy commands (Option A: AddLesson & AddQuiz)

Layer 4: Infrastructure - Consumers (deps: Layer 3)
  └── 3.12 VideoEventHandlers.cs (thin wrappers delegating to MediatR)

Layer 5: Shared (independent)
  └── 3.13 IArnResolver interface + CourseArnResolver implementation

Layer 6: Presentation (deps: all above)
  └── 3.15 Pool endpoints

Layer 7: DI + Migration
  ├── 3.16 DI registration (strategies, repo, resolver)
  └── EF Core migration
```
