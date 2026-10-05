# 05: Image Processing Package Refactor (Strategy Pattern)

## Context

The current image processing logic inside the shared `AlphaZero.ImageProcessing` package applies a uniform resizing strategy to all uploaded images. However, the platform requires distinct optimizations based on the image's context (e.g., Course Covers, Video Thumbnails, Inline Lesson Images, Avatars). To allow the `ImageProcessorAndMoverToR2` Lambda to dynamically process images without bloated conditional logic, the `AlphaZero.ImageProcessing` package must be refactored to use the **Strategy Pattern**.

## Current State

- `src/shared/AlphaZero.ImageProcessing/IImageProcessor.cs` and `ImageSharpProcessor.cs` handle image loading and fixed resizing.
- The Lambda invokes `IImageProcessor.ProcessAsync` which unconditionally outputs three fixed WebP variants (Thumbnail, Medium, Large).
- No concept of "Profiles" or "Contexts" exists in the current package.

## Proposed Change

Refactor the `AlphaZero.ImageProcessing` package to implement context-aware image processing strategies. The Lambda will pass a `ProcessingProfile` string (e.g., `"course_cover"`, `"video_thumbnail"`) to a Strategy Factory, which will resolve the appropriate strategy and execute it. 

### Implementation Details

#### 1. Define Strategy Interface
Create `IImageProcessingStrategy.cs`:
```csharp
namespace AlphaZero.ImageProcessing;

public interface IImageProcessingStrategy
{
    string ProfileName { get; }
    Task<ImageProcessingResult> ProcessAsync(string inputFilePath, string outputDirectory, CancellationToken ct = default);
}
```

#### 2. Implement Concrete Strategies
Implement the following strategies using `SixLabors.ImageSharp`:

1. **CourseCoverProcessingStrategy (`ProfileName = "course_cover"`)**
   - Applies 16:9 Smart Center Crop.
   - Outputs: `card` (600x338, aggressive compression), `hero` (1200x675, standard compression).
2. **VideoThumbnailProcessingStrategy (`ProfileName = "video_thumbnail"`)**
   - Applies 16:9 Smart Center Crop.
   - Outputs: `sidebar` (160x90), `player` (1280x720).
3. **InlineImageProcessingStrategy (`ProfileName = "inline"`)**
   - Preserves Original Aspect Ratio.
   - Outputs: `sm` (max width 300px), `md` (max width 800px), `lg` (max width 1600px).
4. **AvatarProcessingStrategy (`ProfileName = "avatar"`)**
   - Applies 1:1 Square Center Crop.
   - Outputs: `tiny` (48x48), `profile` (256x256).

#### 3. Implement Strategy Factory
Create `ImageProcessorFactory.cs`:
```csharp
namespace AlphaZero.ImageProcessing;

public interface IImageProcessorFactory
{
    IImageProcessingStrategy GetStrategy(string profileName);
}
```
*Note: The factory should default to `InlineImageProcessingStrategy` if an empty or unknown profile is provided.*

#### 4. Update the Shared Package Registration
Update the dependency injection extension (if any) in `AlphaZero.ImageProcessing` to register the factory and all strategies as Singletons or Transients so the Lambda can easily resolve the factory.

## Acceptance Criteria

1. The `IImageProcessingStrategy` interface and `IImageProcessorFactory` are fully implemented in the shared package.
2. Four concrete processing strategies exist and correctly implement their respective aspect ratio/cropping rules.
3. The original `ImageSharpProcessor` is either removed or refactored into these strategies.
4. The factory defaults to the `inline` strategy for unrecognized profiles.
5. Code compiles cleanly with zero warnings or errors.

## Testing Plan

| Layer       | What                     | Count |
|-------------|--------------------------|-------|
| Unit        | Factory resolving logic  | +3    |
| Unit        | Specific Strategy outputs| +4    |

## Rollback Plan

Revert the `AlphaZero.ImageProcessing` package changes via Git and restore the original monolithic `IImageProcessor.cs`.

## Effort Estimate

- Interface & Factory setup: 0.5h
- Concrete Strategies implementation: 2h
- Unit Tests: 1h

## Files Reference

| File | Change |
|------|--------|
| `src/shared/AlphaZero.ImageProcessing/IImageProcessingStrategy.cs` | New interface |
| `src/shared/AlphaZero.ImageProcessing/IImageProcessorFactory.cs` | New factory |
| `src/shared/AlphaZero.ImageProcessing/Strategies/*.cs` | Concrete strategy implementations |
| `src/shared/AlphaZero.ImageProcessing/ImageSharpProcessor.cs` | Refactor/Delete |

## Out of Scope

- Implementing the Lambda handler itself (this is handled in Ticket #04).
- Modifying the Document API or Database Schema.

## Related

- #04 — Serverless Document Processing Pipeline
- #02 — Document Metadata and Variants
