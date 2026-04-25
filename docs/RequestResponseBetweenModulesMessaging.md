# 🪐 Architectural Pattern: Materialized Resource Snapshot

This document outlines the standard pattern for cross-module communication and data synchronization within the AlphaZero Modular Monolith. 

## 🎯 Objective
To enable the **Courses Module** (or any orchestrator) to return a fully populated resource tree (Videos, Quizzes, Documents) in a **single database query**, maintaining high performance while strictly respecting module boundaries.

---

## 🏗️ 1. The Domain Store (Orchestrator Side)
The orchestrating module stores a "Snapshot" of the metadata it needs from the external resource.

- **Storage:** Use a `ResourceId` (Guid) and a `Metadata` (`jsonb` / `JsonElement`) column.
- **Domain Logic:** The aggregate root (e.g., `Course`) must provide a method to update these snapshots:
  ```csharp
  public void UpdateResourceMetadata(Guid resourceId, JsonElement metadata) { ... }
  ```

## 📡 2. Creation Orchestration (Request/Response)
Used for "One-Click" actions where an orchestrator creates a resource in a provider module.

1.  **Contract:** Define `Create{Resource}Request` and `Response` in the `IntegrationEvents` project of the **Provider**.
2.  **Service Wrapper:** Create an interface (e.g., `IAssessmentService`) in the **Orchestrator's Infrastructure** using MassTransit's `IRequestClient`.
3.  **Handler:** The Command Handler in the Orchestrator calls this service, receives the ID/Metadata, and saves the link + snapshot in its own database.

## 🔄 3. Synchronization (Event-Driven)
Ensures the "Snapshot" in the Orchestrator remains consistent if the source changes.

1.  **Fact (Provider):** The provider aggregate publishes a **Domain Event** (e.g., `AssessmentPublishedDomainEvent`).
2.  **Bridge:** A Domain Event Handler in the Provider publishes an **Integration Event** (e.g., `AssessmentMetadataChangedIntegrationEvent`).
3.  **Sync Consumer (Orchestrator):** A MassTransit Consumer in the Orchestrator's Infrastructure listens for this fact.
4.  **Application Update:** The Consumer sends a `SyncResourceMetadataCommand` to its own Application Layer.
5.  **Pipeline:** The MediatR pipeline (via `UnitOfWorkDecorator`) handles the transaction and database commit.

---

## 📹 Application Example: Video Module

When applying this to the **VideoUploading** module:

1.  **Orchestrator (Courses):** `CourseSectionLesson` stores a snapshot of the video status, duration, and thumbnail.
2.  **Provider (VideoUploading):** Once transcoding is complete, it publishes `VideoPublishedEvent`.
3.  **Linkage:** The `VideoProcessingConsumer` in Courses catches this, maps the duration and URL into a JSON snapshot, and updates the local Course DB.
4.  **Result:** The Student API returns the video's "Duration" and "Playable" status without ever querying the Video module's database.

---

## ✅ Benefits
- **Performance:** Zero cross-module requests or Joins during high-traffic read operations.
- **Resilience:** The Orchestrator can function in "Degraded Mode" (using snapshots) even if the Provider is offline.
- **Microservices Ready:** This pattern transitions seamlessly to microservices by swapping in-memory transport for a message broker (SQS/RabbitMQ).
