# Architectural RFC & Peer Review: Unified Curriculum Engine & Cross-Module Asset Authorization

**Platform:** AlphaZero Learning Academy (Multi-tenant Modular Monolith, Enterprise Clean Architecture)  
**Target:** [Course-Architecture-Review-Prompt.md](file:///home/azero/Desktop/AlphaZeroLearningAcademy/plans/Course-Architecture-Review-Prompt.md)  
**Review Version:** 3.0 (Cloudflare Free CDN & Course Resource Pool Integration)

---

## 1. Executive Summary & Core Design Drivers

AlphaZero is optimized for low-bandwidth environments with zero unnecessary bandwidth costs. It combines two authoring workflows for educators, a staging pool for asynchronous video transcoding, and a zero-coupling authorization model:

1. **Teacher Flow 1 (In-Situ Instant Upload & Staging Pool):** Uploading media directly while structuring a course. The saga processes uploads asynchronously in the background, landing ready assets into the **Course Resource Pool (Staging Tray)** for assignment.
2. **Teacher Flow 2 (CMS-First / Asset Library):** Batch-uploading media to tenant root storage first, then referencing their canonical URNs/ARNs across courses.
3. **Course Resource Pool Lifecycle:** Transcoded course assets land in a staging pool. When assigned to a lesson, they leave the pool. When removed from a lesson, they return to the pool. A course cannot be published with dangling/unassigned pool items (preventing loose ends).
4. **Cloudflare Free CDN Architecture:** All video streaming manifests and `.ts` segments are cached and distributed via **Cloudflare CDN (100% free egress)**. Decoupled encryption (ClearKey/AES-128) gates access through the API license key endpoint, ensuring zero unauthorized views.
5. **Decoupled O(1) IAM Authorization:** Permitting students to access assets based on their course enrollment (`az:*:{tenant}:course/{courseId}/*`) via a contextual ACL without per-video IAM rows and without turning `Courses` into a video streaming proxy.

---

## 2. The Teacher Experience & Course Resource Pool

The teacher experience combines direct S3 multipart uploads with a course-level staging pool and a tenant asset catalog.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                               TEACHER AUTHORING STUDIO                                 │
│                                                                                        │
│   ┌─────────────────────────────────────┐    ┌──────────────────────────────────────┐  │
│   │ FLOW 1: Course-Level Upload         │    │ FLOW 2: Tenant Asset Library         │  │
│   │                                     │    │                                      │  │
│   │ 1. Teacher uploads video tagged     │    │ 1. Teacher uploads assets in bulk to │  │
│   │    with target courseId.            │    │    tenant root catalog.              │  │
│   │ 2. Background Saga transcodes media │    │ 2. In Course Builder, clicks         │  │
│   │    (2-5 mins) via MediaConvert.     │    │    "Select from Asset Library".      │  │
│   │ 3. On completion, event lands video │    │ 3. Drawer queries tenant catalog:    │  │
│   │    in the COURSE RESOURCE POOL.     │    │    GET /api/video-uploading?ready    │  │
│   └──────────────────┬──────────────────┘    │    GET /api/documents                │  │
│                      │                       │ 4. Passes existing canonical ARN.    │  │
│                      ▼                       └──────────────────┬───────────────────┘  │
│   ┌────────────────────────────────────────────────────────┐    │                      │
│   │ COURSE RESOURCE POOL (Staging Tray)                    │    │                      │
│   │  - 📹 Lecture 1 Intro (Ready)   [Assign to Section]    │    │                      │
│   │  - 📹 Lab Experiment 1 (Ready)  [Assign to Section]    │    │                      │
│   │  - 📹 Clip B (Unused)           [Dismiss to Library]   │    │                      │
│   └──────────────────┬─────────────────────────────────────┘    │                      │
│                      │ Teacher assigns from pool                │                      │
│                      ▼                                          │                      │
│   ┌────────────────────────────────────────────────────────┐    │                      │
│   │ COURSE CURRICULUM SYLLABUS                             │    │                      │
│   │  Section 1: Basics                                     │    │                      │
│   │   └─► Lesson 1: Lecture 1 Intro ◄───────────────────────────┘                      │
│   │       (Claimed from pool; returns to pool if unassigned)                           │
│   └────────────────────────────────────────────────────────────────────────────────────┘
│                                                                                        │
│   PUBLISHING GUARD: Course cannot be published if items remain in the resource pool.   │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### Key Lifecycle Rules of the Course Resource Pool:
1. **Immune to Race Conditions:** Transcoding takes minutes while UI changes take seconds. By landing in the Course Pool first, changes to section IDs or titles in the course builder never break in-flight video uploads.
2. **Zero Media Loss on Unlink:** Removing a curriculum item restores its primary resource to the Course Resource Pool instead of deleting the video or creating an orphan.
3. **Publishing Invariant:** Courses require zero loose ends before publishing. A teacher must either assign all pool items to lessons or click *"Dismiss to Tenant Library"*, which clears them from the course pool while retaining them in the general tenant asset catalog.

---

## 3. The IAM Dilemma & Solution: Contextual ARN Resolution via ACL

### The Solution
Keep the streaming endpoint in `VideoStreaming` (or media modules), but pass the container context:
`GET /api/videos/{videoId}/stream?courseId={courseId}`

Before IAM evaluates, a lightweight **Anti-Corruption Layer (ACL)** verifies containment and translates the canonical ARN into a **contextual hierarchical ARN**:

```
Client (Student)
  │
  │ 1. GET /api/videos/{videoId}/stream?courseId={courseId}
  ▼
VideoStreaming Module / Endpoint
  │
  │ 2. Resolve Contextual ARN via ACL
  ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│ Course-Asset ACL (Cross-Module Application Service)                          │
│                                                                              │
│ a. Verify Association:                                                       │
│    Does Course(courseId) contain Resource(videoId) in active curriculum?     │
│    (Rejects spoofed courseId requests with 403 Forbidden)                    │
│                                                                              │
│ b. Synthesize Contextual Hierarchical ARN:                                   │
│    From: az:video:{tenantId}:video/{videoId}                                 │
│    To:   az:video:{tenantId}:course/{courseId}/video/{videoId}               │
└──────────────────────────────────────┬───────────────────────────────────────┘
                                       │
                                       │ 3. Evaluate IAM Policy
                                       ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│ AlphaZero IAM Engine (FastEndpoints Preprocessor)                            │
│                                                                              │
│ Required Action: video:Stream                                                │
│ Resource ARN:    az:video:{tenantId}:course/{courseId}/video/{videoId}       │
│                                                                              │
│ Student Policies:                                                            │
│ Policy:          Allow Action="video:*" Resource="az:*:{tenant}:course/{id}/*│
│ Result:          WILDCARD MATCH -> ALLOW                                     │
└──────────────────────────────────────┬───────────────────────────────────────┘
                                       │
                                       │ 4. Return Stream Info (Cloudflare CDN URL + License)
                                       ▼
Client fetches HLS from Cloudflare CDN Edge & License from API
```

---

## 4. Cloudflare CDN Streaming Topology (Zero Egress Bandwidth)

### Architecture Overview
Instead of AWS CloudFront, AlphaZero uses **Cloudflare CDN** in front of S3/R2 storage:
- **Cost:** **100% Free CDN bandwidth** for cached static video segments.
- **Protection Mechanism:** ClearKey / AES-128 DRM.
  - Video manifest & encrypted segments (`.ts`) are publicly cached on Cloudflare CDN edge nodes.
  - The segments cannot be played without the decryption key.
  - The key is served exclusively by `/api/video/keys/{videoId}?courseId={courseId}`, which requires authenticated IAM authorization.

```
[Student Device] ──(1. Get Stream Info)──► [AlphaZero API]
       │                                         │
       │                                         ▼
       │                              Validates ACL & IAM,
       │                              returns Cloudflare CDN URL + License URL
       │                                         │
       ├─────────────────────────────────────────┘
       │
       │ 2. Stream video from Cloudflare (FREE BANDWIDTH)
       ▼
[Cloudflare CDN Edge Cache]
       │
       ├─► GET https://cdn.alphazero.com/streaming/{id}/master.m3u8 (Cached at edge)
       ├─► GET https://cdn.alphazero.com/streaming/{id}/720p.m3u8   (Cached at edge)
       ├─► GET https://cdn.alphazero.com/streaming/{id}/seg001.ts   (Cached at edge)
       └─► ... all video segments served 100% free by Cloudflare ...
       │
       │ 3. Fetch Decryption Key (Gated by IAM)
       ▼
[AlphaZero API Key Endpoint]
       GET /api/video/keys/{videoId}?courseId={courseId}
       (Evaluates student enrollment policy before releasing AES/ClearKey key)
```

### Performance & Load
- **API Server Hits:** Exactly **2 calls** per playback session:
  1. `GET /api/videos/{videoId}/stream?courseId={courseId}` (Manifest location)
  2. `GET /api/video/keys/{videoId}?courseId={courseId}` (Key retrieval)
- **Segment Delivery:** 100% handled by Cloudflare CDN edge with **zero server CPU and zero bandwidth egress cost**.

---

## 5. Architectural Sign-Off

| Domain Area | Evaluation | Decision |
|---|---|---|
| **Authoring UX** | In-situ upload + Course Staging Pool | Asynchronous MediaConvert uploads land in pool; teacher assigns to syllabus with zero race conditions. |
| **Asset Authorization** | Contextual ARN Resolution via ACL | Eliminates Courses proxy; enables O(1) IAM wildcard evaluation on course level. |
| **CDN Provider** | Cloudflare CDN (Free Egress) | Replaces CloudFront. Video segments cached for free; gated by ClearKey API key endpoint. |
| **Progress Bitmask** | Monotonic bitmask with active item count | Phase 1 complete: percentage calculates against active non-deleted items. |
| **Documents Module** | Standalone Clean Module | Phase 2 complete: full CRUD, presigned S3 upload/download, and tenant catalog endpoints. |
