# Dual-Bucket Multi-CDN Architecture

## Problem Statement
Currently, all documents and media assets (like course covers and private PDFs) are uploaded to a single private S3 bucket and processed into a single private Cloudflare R2 bucket. This means that even inherently public assets (like course cover images or generic public documents) require pre-signed URLs and backend authorization to view. This adds unnecessary overhead to public asset delivery and increases backend load, preventing clients from directly leveraging a fast, public CDN for non-sensitive files.

## Solution
Implement a **Dual-Bucket Multi-CDN Architecture**. We will introduce an `IsPublic` flag to the Document domain model and provision two distinct storage pipelines: Public and Private.
- Public assets will be uploaded to a new Public AWS S3 input bucket.
- Private assets will be uploaded to the existing Private AWS S3 input bucket.
- The step function (AWS Step Functions / ImagePipelineStateMachine) will be configured with two triggers: `s3PublicCreated` and `s3PrivateCreated`.
- The processor lambdas will route files to either a Public Cloudflare R2 bucket (served via a public CDN domain without auth) or a Private Cloudflare R2 bucket (served via Pre-Signed URLs), based on the input bucket source.

## User Stories
1. As an instructor, I want to upload a public course cover image, so that prospective students can see it instantly via a fast public CDN without backend authorization delays.
2. As a platform administrator, I want public assets to be served from a separate public bucket, so that I can reduce the cost and overhead of generating signed URLs for non-sensitive data.
3. As a student, I want public images and documents to load instantly, so that my browsing experience is fast even on low-bandwidth connections.
4. As a tenant administrator, I want my private course documents to remain secure in a private bucket, so that they are only accessible to enrolled students via temporary pre-signed URLs.
5. As a backend developer, I want the AWS Step Function to natively distinguish between public and private uploads via separate S3 triggers, so that I do not have to write complex routing logic based on metadata.
6. As a system architect, I want the EventBridge rules to fire `s3PublicCreated` and `s3PrivateCreated` independently, so that processing pipelines can easily adapt to the security context of the upload.
7. As a frontend developer, I want the API to provide direct public URLs for public assets, so that I don't have to asynchronously fetch pre-signed URLs for simple images.

## Implementation Decisions
- **Infrastructure (CDK in `AlphaZero.Cdk`):**
  - Modify `StorageStack.cs` to provision two raw upload buckets: `InputBucketPrivate` and `InputBucketPublic`.
  - Provision two R2 destination buckets (via Cloudflare UI/Terraform) and store their configurations in SSM `R2Credentials` (e.g., updating the JSON to include `PublicBucketName` and `PrivateBucketName`).
  - Modify `ImagePipelineConstruct.cs` to attach two EventBridge rules to the `AlphaZeroImagePipelineStateMachine`: one for `InputBucketPrivate` and one for `InputBucketPublic`.
  - Update Lambda Execution Roles within `ImagePipelineConstruct.cs` to explicitly grant read permissions from `InputBucketPublic` and write permissions to the new Public R2 bucket.
- **Lambda Processors (`src/lambdas/`):**
  - Update `DocumentUploadedS3EventParser` (`InputS3ImageUploadedEventParser`): Extract the source bucket name. If the bucket name matches the public bucket, output `IsPublic = true` in the parsed event JSON.
  - Update `ImageProcessorAndMoverToR2`: Read `IsPublic` from the state machine payload. Move the processed file to the Public R2 Bucket if `IsPublic` is true, otherwise to the Private R2 Bucket.
- **Domain & Application (`Modules.Documents`):**
  - Add `bool IsPublic` to `AlphaZero.Modules.Documents.Domain.Models.Document`. Update `Document.Create` and constructor to accept and set this flag.
  - Create an EF Core database migration (`dotnet ef migrations add`) to add the `IsPublic` column to the Documents table with a default value of `false`.
  - Add `IsPublic` to `VerifyDocumentDeduplicationCommand`, `DocumentProcessingCompletedEvent`, and `CompleteDocumentProcessingCommand`.
  - **Deduplication Scope:** Update the deduplication logic in `VerifyDocumentDeduplicationCommandHandler` to include `IsPublic` in the uniqueness check (i.e., a document is only a duplicate if both the file hash and `IsPublic` flag match).
  - Update `GetDocumentDownloadUrlQueryHandler` / `GetDocumentDownloadUrlEndpoint`: If `document.IsPublic` is true, return the direct public CDN URL (e.g., `https://public-cdn.alphazero.com/...`) instead of evaluating permissions and generating a pre-signed URL.
- **Presentation (`RequestUpload` / `GenerateUploadUrl`):**
  - Update the upload request DTO to include `bool IsPublic = false`.
  - When generating the AWS S3 pre-signed *upload* URL, choose the `InputBucketPublic` if `IsPublic` is true, otherwise use `InputBucketPrivate`.

## Testing Decisions
- **Unit Tests:**
  - `DocumentTests`: Verify `IsPublic` state is correctly persisted when creating a Document.
  - `DocumentUploadedS3EventParserTests`: Feed mocked S3 events for both public and private buckets, asserting `IsPublic` is accurately parsed from the bucket name.
  - `GetDocumentDownloadUrlQueryHandlerTests`: Ensure that for `IsPublic == true`, the public URL is returned without invoking authorization policies, and for `IsPublic == false`, it still correctly checks permissions and generates a signed URL.
- **Integration Tests:**
  - Verify that `GenerateUploadUrl` returns a pre-signed URL for the correct AWS S3 bucket based on the `IsPublic` flag.
  - Verify that Document creation via the Saga correctly maps the `IsPublic` flag.

## Out of Scope
- Migrating existing documents from the private bucket to the public bucket.
- Implementing image resizing or on-the-fly transformations for the public CDN (this will be handled in a separate feature/skill).
- Multi-CDN routing for Videos (Videos will remain strictly on their current streaming CDN architecture).

## Further Notes
- This dual-bucket architecture establishes a robust foundation for building an optional "Image CDN" later, as public images are now physically separated from private document assets.
- We will need to update the SSM parameter `/AlphaZero/VideoPipeline/R2Credentials` to contain JSON with both `PublicBucket` and `PrivateBucket` keys, or add a new SSM parameter for the public R2 bucket.
