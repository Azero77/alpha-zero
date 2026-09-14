# Backend MVP Gaps & TODOs

Based on the `MVP-Score.md`, `PRD.md`, and the upcoming Web (Next.js) & Mobile (React Native) implementation, here are the architectural gaps identified in the current backend endpoints. These must be addressed to unblock the frontend and mobile development.

---

## 5. Offline Video Caching Infrastructure
- **Gap:** The mobile app needs to download videos to bypass poor connectivity, but securely. 
- **Why it is needed:** 
  - *Mobile App (React Native):* The app uses `react-native-video` to download HLS streams (`.m3u8` and `.ts` chunks). If the streaming URLs are highly secure/expiring too fast, the background downloader will fail.
- **Action:** 
  - Ensure the `GetStreamingInfo` or `GetVideoKey` endpoints provide signed URLs for the HLS manifest that either:
    1. Have a long enough expiration time to allow downloading over a slow 3G connection.
    2. Provide a specific "Offline Manifest" payload that the mobile app can parse and download securely without risking URL expiration mid-download.

## 6. Topic-Based Message Routing / Broker Publishing (Future Broker Selection)
- **Context:** Currently, cross-module events are published via MassTransit's in-memory bus (`IModuleBus.Publish`). Consumers in `Courses` use `TargetResourceArn` inspection guards (for lifecycle events) and indexed null-checks (for metadata events) to filter out non-course assets.
- **Goal:** Once a production message broker is selected (e.g., AWS SNS/SQS topic subscriptions or RabbitMQ topic exchanges):
  - Configure topic-based routing derived from `TargetResourceArn` (e.g., `courses.video`, `blog.video`, `tenant.video`).
  - Allow subscriber queues to bind only to specific routing keys / topics (e.g., the Courses module queue binds to `courses.*`).
  - Avoid broadcasting workflow lifecycle events across all module consumers and reduce serverless invocation overhead.

