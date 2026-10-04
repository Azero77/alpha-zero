## Context

The `Documents` module serves as a highly scalable Digital Asset Management (DAM) system optimized for low-bandwidth environments (Syria/MENA). When images are uploaded, they must be processed to extract metadata (dimensions, EXIF) and generate responsive WebP variants. Because this is an API with strict memory/performance constraints, media processing must be offloaded to AWS Step Functions entirely.

## Current State

Currently, the `Documents` module has a basic `DocumentProcessingSaga` and SQS consumers that expect a clean, structured JSON payload upon completion, but the Step Function and the Image Processing Lambda do not yet exist. The `AlphaZero.ImageProcessing` package exists but only supports basic resizing, hardcoded JPEG output to a local disk path, and lacks metadata extraction or WebP support.

## Proposed Change

1. **Step Function Invocation**: Explicitly trigger the AWS Step Function from the `ProcessDocumentUploadedCommand` handler via the AWS SDK.
2. **Lambda Structure**: A monolithic **.NET 10** Lambda (`ProcessImageLambda`) that manages the entire image lifecycle for a document in a single execution context.
3. **Core Engine Upgrades**: Extend `AlphaZero.ImageProcessing` (which uses ImageSharp) to support WebP encoding, introduce an `IImageMetadataExtractor`, and refactor `IImageScaler` to operate on in-memory streams.
4. **S3 In-Memory Streaming**: The Lambda reads from `InputS3` directly into `MemoryStream`, bypasses local `/tmp` disk to avoid storage limits and I/O latency, and writes results back to `CdnS3`.
5. **Output Variants**: Generate multiple responsive WebP variants (e.g., thumbnail, medium, large) to `CdnS3` to support low-bandwidth mobile clients.
6. **Metadata Return Shape**: Push a strongly typed JSON payload (with dimensions and variants mapped) back to `DocumentProcessingCompletedQueue` for the saga to ingest into the `Document` EF Core JSONB column.
7. **Infrastructure**: Define the Step Function and Lambda using AWS CDK in `infrastructure/AlphaZero.Cdk` mimicking the video pipeline.

### Implementation Details

1. Update `AlphaZero.ImageProcessing.csproj` to add WebP support (if needed, though ImageSharp supports it natively via `SaveAsWebpAsync`).
2. Add `IImageMetadataExtractor` to return `Width`, `Height`, and `Format`.
3. Update `IImageScaler` to take and return `Stream` instead of file paths.
4. Create `src/lambdas/AlphaZero.ImageProcessing.Lambda` with a single entry point handling S3 streaming.
5. CDK stack updates: Add `ImagePipelineConstruct.cs` mapping the Lambda and creating the Step Function.
6. Call `AmazonStepFunctionsClient.StartExecutionAsync` inside `ProcessDocumentUploadedCommand`.

## Acceptance Criteria

1. `ProcessDocumentUploadedCommand` successfully triggers the Step Function execution.
2. `AlphaZero.ImageProcessing` processes streams in-memory (no `File.WriteAllBytes` or local `/tmp` usage in Lambda).
3. Uploading an image results in responsive WebP variants appearing in the `CdnS3` bucket.
4. The Lambda successfully sends the Completion JSON back to SQS.
5. The `Document` entity successfully saves the Dimensions and Variants into its `Metadata` JSONB column.

## Testing Plan

| Layer       | What                     | Count |
|-------------|--------------------------|-------|
| Unit        | `AlphaZero.ImageProcessing` stream handling and WebP output | +3    |
| Integration | Lambda execution (simulating S3 get/put) | +2    |
| E2E         | Upload -> Step Function -> Metadata saved in DB | +1    |

## Rollback Plan

Revert the PR. Since the original image remains in `InputS3`, any bug in the lambda will simply leave the document in `Processing` or `Faulted` state, which can be re-triggered.

## Effort Estimate

- ~2h: `AlphaZero.ImageProcessing` upgrades
- ~2h: Lambda implementation (`src/lambdas`)
- ~1h: `ProcessDocumentUploadedCommand` Step Function invocation
- ~1.5h: AWS CDK Infrastructure (`ImagePipelineConstruct.cs`)
- ~1.5h: Testing and validation

## Files Reference

| File | Change |
|------|--------|
| `src/shared/AlphaZero.ImageProcessing/IImageScaler.cs` | Refactor to `Stream` |
| `src/shared/AlphaZero.ImageProcessing/ImageSharpScaler.cs` | Refactor to `Stream`, add WebP |
| `src/lambdas/AlphaZero.ImageProcessing.Lambda/Function.cs` | New monolithic lambda |
| `src/alphazero-api/Modules/Documents/Application/Commands/ProcessSqsMessages/ProcessSqsCommands.cs` | Trigger Step Function |
| `infrastructure/AlphaZero.Cdk/Constructs/ImagePipelineConstruct.cs` | New CDK infrastructure |

## Out of Scope

- Video or PDF metadata extraction (handled separately).
- Re-processing old documents already uploaded (will require a backfill script later).

## Related

- Context from Phase 1 Video Pipeline DAM updates.
