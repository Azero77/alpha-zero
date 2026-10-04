# Implementation Plan: Serverless Image Processing Pipeline (DAM)
**Issue**: #25
**Branch**: ContentManagement

## Context & Goals
The `Documents` module serves as a highly scalable Digital Asset Management (DAM) system optimized for low-bandwidth environments (Syria/MENA). When images are uploaded, they must be processed to extract metadata (dimensions, EXIF) and generate responsive WebP variants. Because this is an API with strict memory/performance constraints, media processing must be offloaded to AWS Step Functions entirely.

## Architectural Mandates & Decisions
During the engineering review, the following key decisions were made to ensure stability, performance, and adherence to clean architecture:

1. **Lambda Disk I/O vs In-Memory**: The Lambda must download the image from `InputS3` to `/tmp`, process it, save WebP variants to `/tmp`, and then upload to `CdnS3`. **DO NOT use in-memory `MemoryStream` for the entire process**, as large pixel buffers will cause OOM crashes.
2. **Single-Pass Processing**: Implement a single-pass `IImageProcessor` that loads the image once, extracts metadata (Dimensions + EXIF), and generates all variants (thumbnail, medium, large). Do NOT parse the image multiple times.
3. **Step Function Error Routing**: Add `Catch` blocks in the Step Function to route Lambda crashes/timeouts to the `DocumentProcessingFaultedQueue`. Unhandled failures must not leave documents permanently "Processing".
4. **Lambda Compute Allocation**: Provision the Lambda with **1536MB of memory** in CDK to ensure it gets a full vCPU core, required for fast ImageSharp processing.
5. **Decoupled Trigger**: Use MassTransit (`IPublishEndpoint`) from the `ProcessDocumentUploadedCommand` handler to publish an event that triggers the Step Function (via EventBridge/SQS). **DO NOT use `AmazonStepFunctionsClient` directly in the API handler**.

## Implementation Tasks

### [T1] `AlphaZero.ImageProcessing` — Implement single-pass `IImageProcessor`
- **Files**: `src/shared/AlphaZero.ImageProcessing/IImageProcessor.cs`, `src/shared/AlphaZero.ImageProcessing/ImageSharpProcessor.cs`
- **Details**: Replace/refactor `IImageScaler` with a new interface `IImageProcessor` that takes an input file path (from `/tmp`) and an output directory. It must return a structured result containing `Width`, `Height`, `Format`, `EXIF` data, and the paths/keys of the generated WebP variants.
- **Requirement**: `Image.LoadAsync` must be called exactly once per image.

### [T2] `ProcessImageLambda` — Implement Lambda with `/tmp` I/O
- **Files**: `src/lambdas/AlphaZero.ImageProcessing.Lambda/Function.cs`
- **Details**: Create a new monolithic .NET 10 Lambda. It should:
  1. Download the S3 object to a local `/tmp/` file.
  2. Invoke `IImageProcessor`.
  3. Upload the resulting WebP files from `/tmp/` to `CdnS3`.
  4. Return a structured JSON payload containing the metadata and variant paths.

### [T3] `AlphaZero.Cdk` — Provision Step Function & Lambda
- **Files**: `infrastructure/AlphaZero.Cdk/Constructs/ImagePipelineConstruct.cs`
- **Details**: 
  - Define the Lambda with **1536MB RAM**.
  - Define a Step Function state machine that invokes the Lambda.
  - On Success: Push the metadata payload to `DocumentProcessingCompletedQueue`.
  - On Failure (`Catch` block): Push a failure payload to `DocumentProcessingFaultedQueue`.

### [T4] `ProcessSqsCommands` — Update to MassTransit Event triggering
- **Files**: `src/alphazero-api/Modules/Documents/Application/Commands/ProcessSqsMessages/ProcessSqsCommands.cs`
- **Details**: Update the command handler. Instead of calling `AmazonStepFunctionsClient`, inject `IPublishEndpoint` and publish a `DocumentProcessingRequestedEvent`. The CDK infrastructure should route this MassTransit event/topic to trigger the Step Function execution.

### [T5] Tests — Add Integration Test for Corrupt Images
- **Files**: `tests/integration/Documents/ImagePipelineErrorTests.cs`
- **Details**: Create an integration test that uploads a corrupted or non-image file. Verify that the Step Function's `Catch` block successfully routes the failure to the `DocumentProcessingFaultedQueue` and the Document entity ends up in the `Faulted` state.

## Final Output Structure Expected
The Step Function should return a strongly typed JSON payload (with dimensions, EXIF, and variants mapped) back to `DocumentProcessingCompletedQueue` for the saga to ingest into the `Document` EF Core JSONB column.
