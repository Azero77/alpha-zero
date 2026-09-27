namespace AlphaZero.Modules.VideoUploading.Application.Models;

public enum PipelineStage
{
    Uploaded = 1,
    Analyzed = 2,
    Prepared = 3,
    Transcoding = 4,
    Distributing = 5,
    Published = 6,
    Failed = 99
}
