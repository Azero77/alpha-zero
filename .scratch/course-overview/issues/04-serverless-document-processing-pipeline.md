# 04: Serverless Document Processing Pipeline & CDK

## Context

The DAM (Digital Asset Management) module requires a highly scalable, serverless pipeline to process uploaded documents (images, PDFs, DOCX). The user has provisioned two new Lambda projects (`InputS3ImageUploadedEventParser` and `ImageProcessorAndMoverToOutputS3`) to be orchestrated by an AWS Step Function. To optimize egress costs and latency, the processed assets must be moved directly to **Cloudflare R2** rather than an output AWS S3 bucket. This ensures students downloading assets fetch them via Cloudflare without incurring AWS data transfer costs.

## Current State

The foundational Lambda projects have been scaffolded with empty implementations (`dotnet new lambda`):
- `src/lambdas/InputS3ImageUploadedEventParser`
- `src/lambdas/ImageProcessorAndMoverToOutputS3`
The CDK infrastructure for the Step Function and R2 integration is not yet fully wired for these specific lambdas. The R2 credentials exist in `StorageStack.cs` (`R2Credentials` SSM parameter).
- You can make use of The video pipeline construct and follow the same pattern because they look similar.

## Proposed Change

We will implement the Step Function workflow and the Lambda handlers:
1. **Parser Lambda**: Parses the incoming EventBridge/S3 Event and formats it into a uniform Step Function payload.
2. **Processor & Mover Lambda**: 
   - Downloads the raw image from the AWS S3 Input Bucket to `/tmp`.
   - Uses the existing `ImageSharpProcessor` (or strategy pattern) to generate WebP variants.
   - Uploads the variants **directly to Cloudflare R2** using the S3 SDK configured with R2 endpoint and credentials.
   - Note: We should rename the project/folder to `ImageProcessorAndMoverToR2` to reflect this architectural shift.
3. **CDK & Step Function**: Wires the parser and processor in a Step Function, utilizing the `R2Credentials` SSM parameter for the Mover Lambda.
#### Note 
- For Step function contracts between each lambda, you can follow the video pipeline pattern where we put all requests and response in one place in order to achieve easier reading.
### Implementation Details

#### 1. Rename Project
- Rename `ImageProcessorAndMoverToOutputS3` to `ImageProcessorAndMoverToR2`.
- Update `.csproj` and folder names accordingly.

#### 2. Implement `InputS3ImageUploadedEventParser`
- **Input**: S3 `ObjectCreated` EventBridge payload.
- **Logic**: Extract the `BucketName` and `ObjectKey`. Parse the `TenantId` and `DocumentId` from the S3 Key (e.g., `tenants/{tenantId}/documents/{docId}/raw.jpg`).
- **Output**: JSON payload `{ "tenantId": "...", "documentId": "...", "s3Bucket": "...", "s3Key": "..." }`

#### 3. Implement `ImageProcessorAndMoverToR2`
- **Input**: Payload from Parser.
- **Logic**: 
  - Fetch `R2Credentials` from SSM Parameter Store (using `AmazonSimpleSystemsManagementClient`). The parameter is `/AlphaZero/VideoPipeline/R2Credentials`.
  - Initialize a new `AmazonS3Client` configured with the R2 endpoint (`https://<accountid>.r2.cloudflarestorage.com`) and credentials.
  - Download raw image to `/tmp`.
  - Process image variants.
  - Upload variants to R2 using the R2 `AmazonS3Client`.
  - Push completion status to MassTransit via EventBridge or SQS.
  - The upload must be in {tenantId}/{document}/[actual variants.jpeg/webp/....]
  - All uploaded image are desired to be in .webp format

#### 4. CDK Step Function (in `DocumentPipelineConstruct` or `ImagePipelineConstruct`)
- Define the two Lambdas. Give the Mover Lambda `ssm:GetParameter` permissions for the R2 credentials.
- Chain them: Parser -> Processor -> Success/Fail Queue.

## Acceptance Criteria

1. The `ImageProcessorAndMoverToOutputS3` project is renamed to `ImageProcessorAndMoverToR2`.
2. The Parser Lambda correctly extracts metadata from the raw S3 event.
3. The Processor Lambda authenticates with Cloudflare R2 using SSM credentials.
4. Processed images (variants) are successfully uploaded to R2, not AWS S3.
5. The Step Function handles errors via `AddCatch` blocks, routing failures to a faulted queue.

## Testing Plan

| Layer       | What                     | Count |
|-------------|--------------------------|-------|
| Unit        | Parser metadata extraction| +2    |
| Integration | Mover Lambda R2 Upload    | +1    |

## Rollback Plan

If the R2 upload integration fails due to credential or latency issues, revert the Processor lambda to use the standard AWS S3 client and an AWS Output Bucket.

## Effort Estimate

- Rename & Cleanup: 0.5h
- Parser Lambda Implementation: 1h
- Mover Lambda (R2 Integration): 2h
- CDK Wiring: 1.5h

## Files Reference

| File | Change |
|------|--------|
| `src/lambdas/ImageProcessorAndMoverToOutputS3/` | Rename to `ImageProcessorAndMoverToR2` |
| `src/lambdas/ImageProcessorAndMoverToR2/Function.cs` | Implement R2 upload and image processing |
| `src/lambdas/InputS3ImageUploadedEventParser/Function.cs` | Implement EventBridge S3 parsing |
| `infrastructure/AlphaZero.Cdk/Constructs/ImagePipelineConstruct.cs` | Update Step Function definition and IAM permissions |

## Out of Scope

- Video processing (handled by a separate pipeline).
- Generating PDF or DOCX previews (handled by a different processor).

## Related

- #25 — Serverless Image Processing DAM
- #02 — Document Metadata and Variants
