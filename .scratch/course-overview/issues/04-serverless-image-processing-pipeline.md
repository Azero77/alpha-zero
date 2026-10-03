# 04: Serverless Image Processing Pipeline (Lambda & Consumer)

**What to build:** A background processing pipeline that automatically scales and optimizes uploaded images into thumbnail, hero, and content-width sizes without slowing down the main API server.

**Blocked by:** 02-document-metadata-and-variants

**Status:** ready-for-agent

- [ ] A new AWS Lambda (`ImageProcessor`) is triggered by S3 PutObject events.
- [ ] The Lambda reads the S3 object metadata (`x-amz-meta-usagecontext`) to decide which sizes to generate.
- [ ] The Lambda generates the correct image variants and uploads them to a processed prefix in S3.
- [ ] The Lambda publishes an `ImageProcessedEvent` to SQS containing the variant URLs.
- [ ] A MassTransit consumer in the monolith receives the event and updates the `Document` record to `Status = Ready` with the variant URLs.
