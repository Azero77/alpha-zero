---
labels: ["ready-for-agent"]
---

## Problem Statement

We have successfully built a complex backend pipeline that handles document/image uploads, processes them into various variants based on specific profiles, and secures them at the Edge using Cloudflare CDN and an IAM-issued Master Course Token. However, we currently lack a quick, visual way to verify that this entire end-to-end flow works correctly from a client's perspective without spinning up the entire production Next.js frontend.

## Solution

We will create a lightweight frontend playground in a new `UIDEMO` directory using Vite and React. This demo will provide a simple UI to upload an image, assign it a profile, and mark it as `IsPublic` (e.g., Course Cover) or Private. It will execute the direct-to-S3 upload flow and render the generated image variants either through the **Public CDN** (for public images) or via **R2 Pre-signed URLs** (for private documents) to prove the end-to-end processing pipeline works perfectly.

## User Stories

1. As a developer, I want to select an image file from my local machine so that I can test the image ingestion pipeline.
2. As a developer, I want to toggle an `IsPublic` flag so that I can test routing files to either the Public Bucket or the Private Bucket.
3. As a developer, I want the UI to automatically request a pre-signed upload URL from the `Documents` module and PUT the file directly to the bucket, accurately mimicking production.
4. As a developer, I want the UI to display all the generated image variants (e.g., Original, Thumbnail, Web-Optimized) side-by-side once the Step Function pipeline finishes.
5. As a developer, I want public variants to be fetched instantly via the Public CDN (`https://cdn...`) and private variants to be fetched securely using the backend `GetDocumentDownloadUrlQuery`.

## Implementation Decisions

- **Framework**: Create a new application inside the `UIDEMO/` directory using Vite and React.
- **Upload Flow Integration**: 
  - The UI will collect the File, Tenant ID (mocked), Scope, Profile, and `IsPublic`.
  - Call `UploadDocument` API to retrieve the S3 Upload Pre-signed URL.
  - Execute a native `fetch` with `method: 'PUT'` to upload the binary file.
- **Rendering & Variant Logic**:
  - Once the upload completes (allowing a few seconds for EventBridge/Step Functions to process), the UI will render the images.
  - For Public images: Construct the URL directly `https://cdn.alphazero.academy/public/{tenantId}/{scope}/{documentId}/{variant}`.
  - For Private images: Call `GetDocumentDownloadUrlQuery` to securely fetch the temporary R2 Pre-signed URL for viewing.

## Testing Decisions

- **What makes a good test**: This is inherently a manual QA/Dogfooding tool. Automated frontend tests (like Jest or Cypress) are not required for this `UIDEMO` sandbox. 
- **Success Criteria**: Success is defined by a developer running the app, uploading an image, and successfully viewing the locked-down variants rendered in their browser without 403 errors from Cloudflare.

## Out of Scope

- Automated UI testing.
- Production-grade CSS/Styling (basic functional layouts or simple Tailwind is sufficient).
- Integrating this specific code into the main Next.js monolithic frontend repository (this is strictly a throwaway/sandbox demo).

## Further Notes

- You may need to add a small delay or a manual "Refresh Variants" button in the UI, as the AWS Step Functions image processing pipeline will take a few moments to generate the thumbnails and optimized variants after the S3 upload completes.
