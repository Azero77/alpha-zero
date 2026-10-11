# Implementation Plan: Image Processing Profiles (Strategy Pattern)

## Context & Goal
The DAM (Documents) module currently processes all images uniformly. However, the platform requires different optimizations based on the image's context (e.g., Course Covers, Video Thumbnails, Inline Lesson Images, Avatars). 

To keep the UX seamless for Content Managers, the frontend will automatically pass a `ProcessingProfile` parameter based on where the upload was initiated. The backend Lambda must use the **Strategy Pattern** to dynamically apply the correct cropping, resizing, and variant generation logic without polluting the codebase with massive `switch` statements. The resulting variants will be stored in the Document's `Dictionary<string, object> Metadata` property.

## Processing Profiles & Variants

### 1. Course Cover (`CourseCover`)
* **Goal:** Look great in dense catalog grids (cards) and on course detail pages (hero).
* **Crop Logic:** Forced 16:9 Aspect Ratio (Smart Center Crop).
* **Variants Generated:**
  * `card`: 600x338 (Aggressive WebP compression for catalog lists).
  * `hero`: 1200x675 (Standard compression for detail pages).
  * `blurhash`: A tiny string for UI placeholders on slow connections.

### 2. Video Thumbnail (`VideoThumbnail`)
* **Goal:** Legible at tiny sizes and sharp inside video players.
* **Crop Logic:** Forced 16:9 Aspect Ratio (Smart Center Crop).
* **Variants Generated:**
  * `sidebar`: 160x90 (Tiny variant for playlist/queue sidebars).
  * `player`: 1280x720 (High sharpness for video player poster).

### 3. Lesson Inline (`InlineImage`)
* **Goal:** Preserve the author's original intent (charts, diagrams) while enforcing max-width bounds for the lesson container.
* **Crop Logic:** Preserve Original Aspect Ratio (No cropping, resize to fit within bounds).
* **Variants Generated:**
  * `sm`: Max width 300px (Mobile fallback).
  * `md`: Max width 800px (Default text-column width).
  * `lg`: Max width 1600px (Click-to-zoom modal view).

### 4. Avatar / Logo (`Avatar`)
* **Goal:** Tiny square profiles.
* **Crop Logic:** Forced 1:1 Aspect Ratio (Square Center Crop).
* **Variants Generated:**
  * `tiny`: 48x48 (For navbars and comments).
  * `profile`: 256x256 (For account settings and teacher pages).

---

## Architectural Implementation Steps (Strategy Pattern)

### [Step 1] Define the Strategy Interface
Inside `AlphaZero.ImageProcessing`:
```csharp
public interface IImageProcessingStrategy
{
    string ProfileName { get; }
    Task<ImageProcessingResult> ProcessAsync(string inputFilePath, string outputDirectory, CancellationToken ct);
}
```

### [Step 2] Implement Concrete Strategies
Create separate classes implementing `IImageProcessingStrategy` using SixLabors.ImageSharp:
* `CourseCoverProcessingStrategy` (Applies 16:9 crop, generates `card` and `hero`).
* `VideoThumbnailProcessingStrategy` (Applies 16:9 crop, generates `sidebar` and `player`).
* `InlineImageProcessingStrategy` (Preserves ratio, generates `sm`, `md`, `lg`).
* `AvatarProcessingStrategy` (Applies 1:1 crop, generates `tiny` and `profile`).

### [Step 3] Implement a Strategy Factory
Create an `ImageProcessorFactory` that accepts the requested profile string and returns the correct strategy instance.
```csharp
public class ImageProcessorFactory
{
    // Resolves the correct strategy based on profile name. Fallback to InlineImage if not provided.
    public IImageProcessingStrategy GetStrategy(string profile);
}
```

### [Step 4] Update Pipeline Triggers & Payloads
* Update `ProcessDocumentUploadedCommand` and `DocumentProcessingRequestedEvent` to include a `string ProcessingProfile` property.
* The frontend/API should accept this `profile` query parameter during the S3 Presigned URL generation or API upload phase, saving it to the `Document` entity so it can be passed to MassTransit.

### [Step 5] Lambda Refactoring
Update the `ProcessImageLambda` to:
1. Read the `ProcessingProfile` from the incoming EventBridge payload.
2. Request the correct strategy from the `ImageProcessorFactory`.
3. Execute the strategy.
4. Return the generated variants dictionary back to the `DocumentProcessingCompletedQueue`.
