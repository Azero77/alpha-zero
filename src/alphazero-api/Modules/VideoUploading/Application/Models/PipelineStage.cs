namespace AlphaZero.Modules.VideoUploading.Application.Models;

public enum PipelineStage
{
    Uploaded = 1,
    Analyzing = 2,
    Preparing = 3,
    Transcoding = 4,
    Publishing = 5,
    Published = 6,
    Failed = 99
}
