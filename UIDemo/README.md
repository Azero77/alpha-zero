# AlphaZero UI Upload Demo

This is a professional React + TypeScript demo application designed to test the S3 Pre-signed URL upload flow and verify SQS/MassTransit event triggers.

## Architecture

1.  **Request Signature**: UI calls the backend API (`/api/courses/upload`) to request a pre-signed URL.
2.  **Direct Upload**: UI uploads the file directly to S3 using the provided URL via a `PUT` request.
3.  **Event Trigger**: S3 triggers an event notification to the SQS queue configured in Aspire.
4.  **MassTransit**: The backend `VideoUploadedConsumer` receives the event from SQS.

## Configuration

The application is configured via `.env`.

- `VITE_API_BASE_URL`: The base URL for the AlphaZero API (default: `https://localhost:17016/api`).
- `VITE_BUCKET_REGION`: The AWS region for S3 (default: `eu-north-1`).

## Directory Structure

- `src/config`: Environment configuration management.
- `src/services/api.ts`: API client for interacting with the backend.
- `src/services/s3.ts`: S3 upload logic using pre-signed URLs.
- `src/components/UploadDemo`: UI components and styles for the upload demo.

## Getting Started

1.  Ensure the backend (Aspire AppHost) is running.
2.  Navigate to this directory: `cd UIDemo`.
3.  Install dependencies: `npm install`.
4.  Start the dev server: `npm run dev`.
5.  Open [http://localhost:5173](http://localhost:5173) in your browser.

## Testing MassTransit

1.  Select a video file in the UI.
2.  Click "Start Upload to S3".
3.  Once the upload is successful, check the logs of the `alphazero-api` project in the Aspire Dashboard.
4.  You should see the `VideoUploadedConsumer` being triggered.
