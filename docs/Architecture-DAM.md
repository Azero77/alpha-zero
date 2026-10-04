# Digital Asset Management (DAM) Architecture

This document outlines the architecture for the `Documents` module (and asset management across the platform), heavily inspired by the proven patterns of the **Squidex Headless CMS**. We have adapted Squidex's rich metadata extraction and event-driven design to fit securely into AlphaZero's Enterprise Clean Architecture (EF Core, MassTransit Sagas, and AWS Serverless).

## 1. The Domain Entity (`Document`)

Unlike simple file-upload systems, a true DAM needs to support rich metadata, deduplication, and various asset types. We model our `Document` entity based on the Squidex `Asset` pattern.

### Key Characteristics:
- **Deduplication:** Uses a `FileHash` to prevent the same tenant from storing the exact same 50MB PDF multiple times.
- **Dynamic Metadata:** Instead of hardcoding fields like `Width` or `Duration`, we use a `Dictionary<string, object> Metadata` mapped to a `jsonb` column in EF Core.
- **Categorization:** Uses a `DocumentType` enum (`Image`, `Video`, `Audio`, `Pdf`, `Document`, `Unknown`) to drive frontend rendering.
- **Saga Status:** Tracks its processing state via a `DocumentStatus` enum (`Pending`, `Processing`, `Ready`, `Faulted`).

## 2. The Metadata Extraction Pipeline

Squidex uses an `IAssetMetadataSource` pipeline to probe files (e.g., using FFmpeg or ImageSharp) to extract metadata. We implement this as `IDocumentMetadataSource`.

```csharp
public interface IDocumentMetadataSource
{
    Task EnhanceAsync(DocumentUploadContext context);
}

public class DocumentUploadContext
{
    public Stream FileStream { get; set; }
    public DocumentType Type { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

When an asset finishes processing, this pipeline runs. Multiple extractors (e.g., `ImageMetadataSource`, `PdfMetadataSource`) inspect the `DocumentUploadContext` and populate the `Metadata` dictionary with properties like `Dimensions`, `Duration`, or `PageCount`.

## 3. MassTransit Saga State Machine

Squidex uses strict Event Sourcing for state transitions. In AlphaZero, we achieve this scalable, event-driven state transition using **MassTransit Saga State Machines** backed by EF Core.

The `DocumentProcessingSaga` tracks the entire lifecycle of an upload:
1. **`UploadInitiated`:** The client requests a presigned URL. The Saga starts, creates a `Pending` document, and schedules a **24-hour timeout** (abandoning the saga if the user never uploads the file).
2. **`UploadedToStorage`:** S3 triggers an `ObjectCreated` event via SQS. The Saga transitions to `Processing` and triggers the AWS Step Functions / Metadata Pipeline.
3. **`ProcessingCompleted`:** The serverless pipeline finishes. The Saga populates the final `Metadata` dictionary, marks the document `Ready`, and finalizes (deleting the SQL saga state row).

## 4. Multi-Tenancy & Authorization (The Context Pattern)

Instead of injecting an `ITenantProvider` deep into domain entities, we adopt Squidex's **Immutable Context** pattern for executing commands.

Every API request generates an immutable `Context` object containing:
- The current `TenantId`.
- The User's `ClaimsPrincipal`.
- Calculated `Permissions`.

This context is passed through the Command bus, ensuring that deep domain logic (e.g., "Does this user have quota to upload a 5GB video?") can be evaluated securely with absolutely zero risk of async thread-local state leakage.

## 5. Shared AWS Infrastructure & Decoupling

While the DAM architecture manages the state, heavy lifting (video transcoding, PDF extraction) is offloaded to AWS Step Functions.

To prevent DRY violations while keeping complex modules (like `VideoUploading` and `Documents`) strictly isolated:
- **Shared Storage:** `S3UploadService` logic is centralized in `AlphaZero.Shared`.
- **Shared Queues:** Both modules use a single AWS infrastructure (`raw-assets` bucket, `processed-assets` bucket, `progress-sqs` queue, `published-sqs` queue).
- **The Router Pattern:** A single lightweight MassTransit consumer reads the raw JSON from AWS SQS. It inspects an `AssetType` field and routes the payload as a typed in-memory MediatR/MassTransit event (`VideoProgressUpdated` vs `DocumentProgressUpdated`) to ensure strict module isolation.
