# 📝 VideoUploading Module API Audit

This file details all audited endpoints, request/response models, FluentValidation constraints, and ErrorOr error codes for the VideoUploading module.

---

### 1. `POST /api/video-uploading/upload`
- **File:** `src/alphazero-api/Modules/VideoUploading/Presentation/Features/Upload.cs`
- **Class:** `Upload.Endpoint`
- **Kind:** ASP.NET Core Minimal API (`IEndpoint`)
- **Command:** `UploadCommand`
- **Request DTO:** `Upload.Request(string fileName, string contentType, string title, string? description, string? transcodingMethod, string? encryptionMethod, bool? generateCustomThumbnailUrl, string? targetResourceArn)`
- **Response DTO:** `Upload.Response(Guid videoId, Guid tenantId, string key, string preSignedUrl, string transcodingMethod, string encryptionMethod, Dictionary<string, string> headers, string? thumbnailKey, string? thumbnailPreSignedUrl, Dictionary<string, string>? thumbnailHeaders)`
- **Success Status:** `200 OK`
- **Authorization:** `video:Upload` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`RequestUploadCommandValidator`):**
  - `fileName`: NotEmpty, EndsWith(".mp4")
  - `contentType`: NotEmpty, Equals("video/mp4")
  - `title`: NotEmpty, MaximumLength(255)
  - `transcodingMethod`: IsEnumName(typeof(VideoTranscodingMetehod))
  - `encryptionMethod`: IsEnumName(typeof(VideoEncryptionMethod))
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`):
    - Unauthenticated request
    - `Tenant.NotFound` ("Tenant not found in context.")
  - `403 Forbidden` (`ProblemDetails`): Missing `video:Upload` permission

---

### 2. `GET /api/video/{videoId:guid}`
- **File:** `src/alphazero-api/Modules/VideoUploading/Presentation/Features/Streaming/GetStreamingInfo.cs`
- **Class:** `GetStreamingInfo.Endpoint`
- **Kind:** ASP.NET Core Minimal API (`IEndpoint`)
- **Query:** `GetStreaminInfoForVideoQuery(Guid VideoId)`
- **Request Parameters:** Route parameter `videoId` (Guid)
- **Response DTO:** `StreamingInfoResponseDTO(string url, string? encryptionMethod, string? licenseUrl, DrmInfo? drm)`
- **Success Status:** `200 OK`
- **Authorization:** Standard endpoint
- **Error Codes & Status Mappings:**
  - `404 Not Found` (`ProblemDetails`): `Video.NotFound` ("Video with ID {videoId} was not found.")

---

### 3. `GET /api/video/keys/{VideoId:guid}`
- **File:** `src/alphazero-api/Modules/VideoUploading/Presentation/Features/GetVideoKey.cs`
- **Class:** `GetVideoKeyEndpoint`
- **Kind:** FastEndpoints (`Endpoint<GetVideoKeyRequest>`)
- **Query:** `GetVideoKeyQuery(Guid VideoId)`
- **Request DTO:** `GetVideoKeyRequest { Guid VideoId }`
- **Response Content:** Binary stream (`application/octet-stream`)
- **Success Status:** `200 OK`
- **Authorization:** `video:Stream` on `ResourceArn.ForVideo(tenantId, req.VideoId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `video:Stream` permission
  - `404 Not Found` (`ProblemDetails`): `VideoSecret.NotFound` ("Secret for video {VideoId} not found.")
  - `500 Internal Server Error` (`ProblemDetails`): `VideoSecret.InvalidFormat` ("Stored key is not a valid hex string.")

---

### 4. `PATCH /api/video-uploading/debug/videos/{id:guid}`
- **File:** `src/alphazero-api/Modules/VideoUploading/Presentation/Features/UpdateVideoInfo.cs`
- **Class:** `UpdateVideoInfo.Endpoint`
- **Kind:** ASP.NET Core Minimal API (`IEndpoint`)
- **Command:** `UpdateVideoInfoCommand(Guid VideoId, string Title, string? Description)`
- **Request DTO:** `UpdateVideoInfo.Request(string Title, string? Description)`
- **Response DTO:** None (`Results.NoContent()`)
- **Success Status:** `204 NoContent`
- **Authorization:** `video:Edit` on `ResourceArn.ForVideo(tenantId, id)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `video:Edit` permission
  - `404 Not Found` (`ProblemDetails`): `Video.NotFound` ("Video with ID {id} was not found.")

---

### 5. `api/video-uploading/debug/videos` (CRUD/State)
- **File:** `src/alphazero-api/Modules/VideoUploading/Presentation/Features/Debug.cs`
- **Class:** `Debug` (Contains `GetVideosEndpoint`, `GetVideoEndpoint`, `GetVideoStateEndpoint`, `DeleteVideoEndpoint`)
- **Kind:** ASP.NET Core Minimal API (`IEndpoint`)
- **Endpoints & Status Mappings:**
  - `GET api/video-uploading/debug/videos` (`GetVideosEndpoint`):
    - Query: `ListVideosQuery(int page, int perPage)`
    - Response: `PagedResult<VideoResponse>` (`200 OK`)
    - Auth: `video:List` on `ResourceArn.ForTenant(tenantId)` (401, 403)
  - `GET api/video-uploading/debug/videos/{id:guid}` (`GetVideoEndpoint`):
    - Query: `GetVideoQuery(Guid id)`
    - Response: `VideoResponse` (`200 OK`)
    - Auth: `video:View` on `ResourceArn.ForVideo(tenantId, id)` (401, 403)
    - Error: `404 Not Found` (`Video.NotFound`)
  - `GET api/video-uploading/debug/videos/{id:guid}/state` (`GetVideoStateEndpoint`):
    - Query: `GetVideoStateQuery(Guid id)`
    - Response: `VideoStateDto` (`200 OK`)
    - Auth: `video:View` on `ResourceArn.ForVideo(tenantId, id)` (401, 403)
    - Error: `404 Not Found` (`VideoState.NotFound`)
  - `DELETE api/video-uploading/debug/videos/{id:guid}` (`DeleteVideoEndpoint`):
    - Command: `DeleteVideoCommand(Guid id)`
    - Response: `204 NoContent`
    - Auth: `video:Delete` on `ResourceArn.ForVideo(Guid.Empty, id)` (401, 403)
    - Error: `404 Not Found` (`Video.NotFound`)
