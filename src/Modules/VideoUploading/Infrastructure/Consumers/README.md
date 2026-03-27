# MediaConvert Job Configuration Guide

This directory contains the `job.json` template used by the `StartVideoProcessingCommandHandler`. The configuration is specifically optimized for the **AlphaZero Learning Academy**, targeting low-bandwidth environments (Syria/MENA) while maintaining high educational clarity.

## 📁 File Structure
- `job.json`: The AWS Elemental MediaConvert job template.
- `StartVideoProcessingCommandHandler.cs`: The consumer that injects dynamic S3 paths and ARNs into the template.

---

## 🚀 Optimized Features

### 1. QVBR (Quality-Defined Variable Bitrate)
Instead of a fixed bitrate, we use **QVBR**. This ensures that static scenes (like a teacher standing in front of a whiteboard) use very little data, while complex scenes get the bitrate they need.
- **Benefit:** Reduces CloudFront costs by ~20-40% without sacrificing visual quality.

### 2. Temporal Noise Reduction
We've enabled the `NoiseReducer` with a `TEMPORAL` filter.
- **Why:** User-uploaded content often has sensor noise. Noise is "expensive" to encode.
- **Benefit:** Smoothing out noise allows the encoder to focus bits on the actual content, leading to smaller file sizes.

### 3. Adaptive Quantization (Educational Optimization)
Set to `HIGH`. 
- **Why:** Educational videos often contain high-contrast text on whiteboards.
- **Benefit:** Prevents "blurring" around text and fine lines, ensuring students can read what the teacher writes even at lower resolutions.

### 4. Trick Play (I-Frame Only Manifests)
- **Feature:** `IframeOnlyManifests: "INCLUDE"`.
- **Benefit:** Allows students to see thumbnail previews while scrubbing the video timeline without downloading full video segments. Essential for navigating long lectures.

### 5. Audio Normalization
- **Algorithm:** `ITU_BS_1770_2` (Target: -24 LKFS).
- **Benefit:** Ensures a consistent volume level across different teachers and courses, preventing sudden loudness or whisper-quiet audio.

### 6. CMAF (Common Media Application Format)
We use **CMAF** segments (fragmented MP4) with a 2-second fragment length.
- **Benefit:** Allows for faster playback startup (low latency) while maintaining the compatibility of HLS and DASH.

---

## 📊 Bitrate & Resolution Strategy

| Rendition | Resolution | Max Bitrate | Optimization |
| :--- | :--- | :--- | :--- |
| **1080p** | 1920x1080 | 3.0 Mbps | High-quality archival |
| **720p** | 1280x720 | 1.8 Mbps | Standard desktop viewing |
| **480p** | 854x480 | 800 Kbps | **Syria Baseline** (Best for Mobile) |
| **360p** | 640x360 | 500 Kbps | Extreme low-bandwidth mode |
| **Audio** | N/A | 64 Kbps | Audio-only "Radio" mode |

---

## 🛠 Variables Used
The following placeholders are replaced at runtime by the infrastructure:
- `##INPUT_FILE##`: The source S3 URI.
- `##OUTPUT_PATH##`: The destination S3 folder for HLS/CMAF files.
- `##KMS_KEY_ARN##`: The AWS KMS key used for side-side encryption.
- `##MediaConvertRole##`: The IAM role MediaConvert assumes to access S3.
