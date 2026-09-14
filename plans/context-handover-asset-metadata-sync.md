# Context Handover: Cross-Module Asset Metadata Synchronization & Workflow Event Filtering

## 1. Project Context & Architectural Mandates
- **Platform:** AlphaZero Learning Academy — Multi-tenant SaaS e-learning platform (.NET 10 / C# 12).
- **Architecture Pattern:** Modular Monolith following **Enterprise Clean Architecture**.
- **Module Isolation:** Modules (`Courses`, `VideoUploading`, `Documents`, `Assessments`, `Tenants`, `Identity`) are strictly isolated.
  - **Zero cross-module database joins or queries.**
  - Cross-module communication is strictly asynchronous via **MassTransit (`IModuleBus`)** integration events or internal MediatR for in-process request/response.
- **Tenant Scoping:** Every request and entity is tenant-scoped (`ITenantProvider`, `TenantOwnedEntity`, `TenantOwnedAggregate`).
- **Deployment Agnostic:** The architecture must work identically whether running:
  - On-premise / monolith / tests: using **MassTransit In-Memory Mediator**.
  - Cloud / serverless: using **AWS SQS/SNS**, **RabbitMQ**, or **Azure Service Bus**.
  - No coupling to proprietary broker-specific routing rules or queue configurations in application logic.

---

## 2. Current Git State & Verification
- **Branch:** `CourseCirriculumArchitecture`
- **Working Tree:** Clean (`git status` clean, zero uncommitted code modifications).
- **Test Status:** 
  - `Courses.UnitTests`: 71 passing (100%).
  - `Courses.Tests.Integration` (real PostgreSQL Testcontainers): 25 passing (100%).

---

## 3. What Exists Right Now in the Codebase

1. **Courses Module - Metadata Sync Strategy Pattern:**
   - File: `src/alphazero-api/Modules/Courses/Application/Courses/Commands/SyncResourceMetadata/SyncResourceMetadata.cs`
   - Contains:
     - `SyncResourceMetadataCommand(Guid ResourceId, JsonElement Metadata)`
     - `SyncResourceMetadataCommandHandler`
     - Strategy interface: `ICourseMetadataSyncCommandHandler` with implementations:
       - `VideoCourseMetadataSyncCommandHandler`
       - `DocumentCourseMetadataSyncCommandHandler`
       - `AssessmentCourseMetadataSyncCommandHandler`
   - Verified by 13 unit tests and 3 integration tests.

2. **Courses Module - CourseAsset Model (TPH):**
   - File: `src/alphazero-api/Modules/Courses/Domain/Aggregates/Courses/CourseMediaAsset.cs`
   - Entity: `CourseAsset` (abstract) with subtypes `VideoCourseAsset`, `DocumentCourseAsset`, `AssessmentCourseAsset`.
   - States: `Pending`, `Available`, `InUse`, `Archived`, `Failed`.

3. **Existing Workflow Consumers in Courses:**
   - File: `src/alphazero-api/Modules/Courses/Infrastructure/Consumers/Videos/VideoEventHandlers.cs`
     - `VideoUploadingStartedEventHandler`: Consumes `UploadVideoRequestedEvent`. Already checks `TargetResourceArn` to create a `Pending` `VideoCourseAsset`.
     - `VideoUploadingFailedEventHandler`: Consumes `VideoProcessingFailedEvent`. Currently **lacks** a `TargetResourceArn` check.
     - `VideoUploadedEventHandler`: Consumes `VideoPublishedEvent`. Currently **lacks** a `TargetResourceArn` check.
   - File: `src/alphazero-api/Modules/Courses/Infrastructure/Consumers/AssessmentMetadataChangedConsumer.cs`
     - Consumes `AssessmentMetadataChangedIntegrationEvent` and dispatches `SyncResourceMetadataCommand`.

4. **Source Modules State:**
   - **`Assessments` (Fully Implemented):**
     - `Assessment.cs` emits `AssessmentMetadataUpdatedDomainEvent` in `UpdateInformation(...)`.
     - `AssessmentDomainEventHandlers.cs` catches this and publishes `AssessmentMetadataChangedIntegrationEvent` via `IModuleBus`.
   - **`VideoUploading` (Partially Implemented):**
     - `Video.cs` has `UpdateInformation(title, description)`, but does **NOT** yet emit any domain events.
     - `IntegrationEvents/Events.cs` has `VideoPublishedEvent(VideoId, RelativeUrl, TargetResourceArn)`, but `VideoProcessingFailedEvent` does **NOT** have `TargetResourceArn`.
     - No domain event handler or integration event exists yet for video metadata edits.
   - **`Documents` (Partially Implemented):**
     - `Document.cs` only has `Create` and `MarkAsDeleted`. Has **NO** `UpdateInformation` and emits no domain events.
     - `IntegrationEvents/Events.cs` only has queries (`DocumentExistsQuery`), no metadata changed integration events.

---

## 4. The Exact Problem to Solve in the New Chat

We need to implement the end-to-end synchronization pipeline when assets (`Video`, `Document`, `Assessment`) change, while resolving two critical system design questions:

### Problem 1: False Handlers on In-Flight Workflows (`VideoPublished` / `VideoProcessingFailed`)
- **Scenario:** A video can be uploaded directly for a course (Flow 1: Course upload with `TargetResourceArn = az:course:...`) OR outside the course module (Flow 2: Tenant Asset Library, user avatar, blog, marketing, where `TargetResourceArn` is null or non-course).
- **Requirement:** When background transcoding completes or fails, `Courses` consumers (`VideoUploadedEventHandler`, `VideoUploadingFailedEventHandler`) must handle course uploads without producing false errors, warnings, or DB lookups for non-course videos.
- **Direction:** Propagate `TargetResourceArn` on all lifecycle events (`VideoPublishedEvent`, `VideoProcessingFailedEvent`), and enforce a guard (in consumer or C# MassTransit pipeline filter) so non-course workflows exit immediately in nanoseconds.

### Problem 2: Global Asset Updates & Avoiding Pointless DB Queries on Non-Course Assets
- **Scenario:** In the Tenant Asset Library, thousands of videos, documents, and assessments exist. Only a fraction are linked to courses via `CourseAsset`.
- **Requirement:** When an admin edits a video or document in the Tenant Library:
  1. The entity (`Video.cs`, `Document.cs`, `Assessment.cs`) must raise a domain event (`EntityMetadataUpdatedDomainEvent`).
  2. `UnitOfWork.SaveChangesAsync` harvests and dispatches this in-process via MediatR.
  3. Module Application domain event handlers map it to an integration event (`EntityMetadataChangedIntegrationEvent`) and publish via `IModuleBus`.
  4. `Courses` must consume the event and update the corresponding `CourseAsset`.
- **The Core Question:** How does `Courses` handle incoming global asset changes without querying `CourseAssets` table (`SELECT 1 FROM CourseAssets WHERE AssetId = @id`) for every non-course asset in the academy?
- **Constraints:**
  - Must preserve strict **loose coupling** between modules (`VideoUploading` and `Documents` must remain generic supporting modules and NOT know about `Courses` or create course-specific event types).
  - Must be **deployment-agnostic** (pure C# / MassTransit without depending on RabbitMQ routing keys or cloud-specific filter policies).
  - Caching strategy note: The user plans to implement caching themselves, but the architectural pattern (e.g., in-memory membership filter `ICourseAssetMembershipFilter`, negative caching, or direct repository lookup) needs to be cleanly plugged in.

---

## 5. Scope of Tasks to Implement Next
1. **`VideoUploading` Module:**
   - Add `VideoMetadataUpdatedDomainEvent` to `Video.cs` when `UpdateInformation` is called.
   - Add `VideoDomainEventHandlers : INotificationHandler<VideoMetadataUpdatedDomainEvent>` to publish `VideoMetadataChangedIntegrationEvent`.
   - Add `TargetResourceArn` to `VideoProcessingFailedEvent`.
2. **`Documents` Module:**
   - Add `UpdateInformation` to `Document.cs` emitting `DocumentMetadataUpdatedDomainEvent`.
   - Add `DocumentDomainEventHandlers : INotificationHandler<DocumentMetadataUpdatedDomainEvent>` to publish `DocumentMetadataChangedIntegrationEvent`.
3. **`Courses` Module:**
   - Add `TargetResourceArn` guard to `VideoUploadedEventHandler` and `VideoUploadingFailedEventHandler`.
   - Implement `VideoMetadataChangedConsumer` and `DocumentMetadataChangedConsumer` to call `SyncResourceMetadataCommand`.
   - Integrate the zero-DB/membership guard pattern so non-course assets are filtered efficiently.
4. **Testing:**
   - Unit tests for domain event emission in `Video` and `Document`.
   - Integration tests in `Courses` verifying metadata synchronization and non-course event filtering.
