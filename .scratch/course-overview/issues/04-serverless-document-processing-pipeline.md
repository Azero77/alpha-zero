# 04: Serverless Document Processing Pipeline & CDK

**What to build:** An AWS Step Function-based pipeline to orchestrate the processing of different document types (Images, PDF, DOCX), tracking state via a MassTransit Saga State Machine.

**Blocked by:** 02-document-metadata-and-variants

**Status:** ready-for-agent

- [ ] Create an AWS CDK construct (`DocumentPipelineConstruct` similar to `VideoPipelineConstruct`) defining the `raw-documents` and `processed-documents` buckets, Step Function, and three SQS queues (`s3-put-queue`, `progress-sqs`, `published-sqs`).
- [ ] An S3 `ObjectCreated` event pushes to `s3-put-queue` which initiates the Step Function (or an EventBridge rule directly triggers the Step Function).
- [ ] The Step Function routes execution to the appropriate Processor tasks (Lambda/Fargate) based on metadata (e.g., ImageProcessor, PdfProcessor, DocxProcessor). These upload their variants to `processed-documents`.
- [ ] The pipeline processors push status updates to `progress-sqs`, and the Step Function's final state pushes a completion event to `published-sqs`.
- [ ] Add MassTransit consumers in the API that listen to `progress-sqs` and `published-sqs` to advance the Saga state machine, update the `Document` DB record with final variant URLs, and clean up the Saga state.
