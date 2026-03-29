# 🪐 AlphaZero Architecture Strategy: Multi-Cloud Video Pipeline

This document outlines the strategic decision to use a hybrid AWS/Cloudflare architecture for video processing and delivery. The goal is to maximize profitability for the SaaS owner while ensuring high availability and performance in the MENA region.

## 🎯 The "Solo-Dev" Strategy
As a solo developer and SaaS owner, the architecture priorities are:
1. **Low Engineering Overhead:** Use managed services for complex tasks (AWS MediaConvert).
2. **Zero "Bill Shock":** Eliminate per-GB egress fees for video delivery (Cloudflare R2).
3. **Smart Ingestion:** Inspect source files before processing to prevent expensive upscaling.

---

## 💰 Pricing Comparison (Large Scale)
*Based on a 100-hour course, 200 students, $50 course price, and 1 PB/month total platform traffic.*

| Metric | AWS Native (CloudFront) | AlphaZero Hybrid (R2) |
| :--- | :--- | :--- |
| **Hosting Cost per Student** | **$5.77** | **$1.30** |
| **Total Monthly Cost (1 PB)** | **~$41,150** | **~$1,400** |
| **SaaS Net Profit Margin** | **~88%** | **~97%** |

### Why Cloudflare R2 is the Winner:
- **The "AWS Exit Tax":** AWS charges ~$90/TB to move data out of its network. By using a "Mover" worker to sync from S3 to R2 **exactly once** per video, we avoid paying this fee every time a student watches a video.
- **$0 Egress:** Cloudflare CDN does not charge for bandwidth delivered from R2. This makes the platform immune to "viral" usage or high re-watch rates.

---

## 🏗️ Technical Pipeline: The "Life of a Video"

1. **Ingestion (AWS S3):** Teachers upload raw MP4s to a private S3 bucket.
2. **Analysis (FFProbe):** A background worker inspects the file. If it's 720p, we tell the transcoder **not** to create a 1080p rendition (saving 40% on AWS costs).
3. **Optimization (AWS MediaConvert):** Managed transcoding into encrypted HLS/CMAF segments. Output is sent to a **Transient S3 Bucket**.
4. **Distribution (Cloudflare R2):** A MassTransit "Mover" worker streams the folder from S3 to R2 and then deletes the S3 copy.
5. **Streaming (CDN):** Students stream via a custom domain connected directly to R2, secured by **ClearKey (AES-128) Encryption**.

---

## 🌍 Regional Performance (Syria & MENA)
Cloudflare maintains physical Points of Presence (PoPs) in Iraq, Jordan, Lebanon, and Turkey. This results in significantly lower **Time to First Byte (TTFB)** for Syrian students compared to AWS CloudFront, which primarily serves the region from Bahrain or UAE.

---

## 🛠️ Enforcement & Standards
To ensure this strategy remains robust, the codebase enforces a strict **Command/Event** orchestration pattern via a MassTransit Saga.
- **Commands:** Explicit instructions (e.g., `AnalyzeVideoCommand`, `SyncVideoToCdnCommand`).
- **Events:** Immutable facts (e.g., `VideoMetadataProcessedEvent`).
- **Persistence:** All commands are automatically transactional via the `UnitOfWorkDecorator`.
