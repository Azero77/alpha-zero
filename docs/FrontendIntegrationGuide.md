# 🎨 Frontend Integration Guide: Content & Orchestration

This guide explains how the Frontend should interact with the AlphaZero API to manage course content (Quizzes, Videos, Docs) while respecting module boundaries.

---

## 🏗️ The Architectural Model
AlphaZero uses a **Content Provider vs. Orchestrator** model:
- **Providers (Assessments, VideoUploading):** Handle raw files and logic.
- **Orchestrator (Courses):** Manages the learning path and stores **Snapshots** of metadata for performance.

---

## 📝 1. Adding a Quiz to a Course
Quizzes are handled via **Backend Orchestration**. You only need to make **one call** to the Courses module.

### Workflow:
1. Call the `Courses` module.
2. The Courses module will automatically talk to the `Assessments` module.
3. The Quiz is created and linked in one atomic user action.

**Endpoint:** `POST /courses/{courseId}/sections/{sectionId}/assessments`
**Payload:**
```json
{
  "title": "Module 1 Final Exam",
  "assessmentRequest": {
    "title": "Module 1 Final Exam",
    "type": "MCQ",
    "passingScore": 80,
    "description": "Final exam for the first module."
  }
}
```
**Handling the Result:**
The response will return `204 No Content`. To see the new quiz, refresh the course structure.

---

## 📹 2. Uploading a Video to a Course
Videos use a **Direct Provider Flow** with **Smart Routing** via S3 Metadata.

### Workflow:
1. **Get Presigned URL:** Call the `VideoUploading` module. **Crucial:** You must provide the `targetResourceArn` of the Course/Section/Lesson where this video belongs.
2. **Construct the ARN:** 
   - Add to Section: `az:courses:{tenantId}:course/{courseId}/section/{sectionId}`
   - Update existing Lesson: `az:courses:{tenantId}:course/{courseId}/section/{sectionId}/lesson/{lessonId}`
3. **Upload to S3:** Use the returned URL to `PUT` the file directly to S3.

**Step 1: Request Upload**
`POST /api/video-uploading/upload`
```json
{
  "fileName": "lecture_1.mp4",
  "contentType": "video/mp4",
  "title": "Introduction to Physics",
  "targetResourceArn": "az:courses:global:course/GUID/section/GUID"
}
```
**Step 2: S3 Upload**
Use the `preSignedUrl` from the response to perform a binary `PUT` request.

**Step 3: UI Feedback**
Since transcoding is asynchronous, the video won't appear in the course immediately. 
- **The "Processing" State:** When you fetch the course structure, the lesson metadata will show `"Status": "Processing"` or will be missing until the backend finishes.
- **Auto-Linking:** The backend will automatically link the video to the course once transcoding is 100% complete.

---

## 📖 3. Consuming the Course Structure
To display a course, you only ever need to make **one call**. Do not call the Video or Assessment modules to get basic metadata.

**Endpoint:** `GET /courses/{id}`
**Response Example:**
```json
{
  "id": "...",
  "title": "Advanced Physics",
  "sections": [
    {
      "title": "Basics",
      "items": [
        {
          "type": "Lesson",
          "title": "Intro Video",
          "metadata": {
            "Url": "https://cdn.alphazero.com/video-1/master.m3u8",
            "Status": "Ready",
            "Duration": "12:45"
          }
        },
        {
          "type": "Assessment",
          "title": "Quick Quiz",
          "metadata": {
            "Type": "MCQ",
            "PassingScore": 70,
            "Status": "Published"
          }
        }
      ]
    }
  ]
}
```

### 💡 Implementation Tips:
- **Metadata snapshots:** Always read the `metadata` property inside course items for UI display (passing scores, video URLs, etc.).
- **Progress:** Use the `bitIndex` and the student's `ProgressBitmask` (from the Enrollment API) to determine if a green checkmark should be shown.
- **Resource ARNs:** Use the `ResourceId` if you need to perform deep actions (like updating quiz questions) by calling the provider modules directly.

---

## 🔐 Security Note
All requests require the `X-Tenant-Id` header (or resolved via subdomain). The API uses **Resource-Based Access Control**. If you try to add a video to a course you don't own, the S3 upload request will fail at the backend level.
