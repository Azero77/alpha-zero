using AlphaZero.Modules.VideoUploading.Application.Models;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Sagas;

public class VideoState
{
    public Guid VideoId { get; set; }
    public Guid TenantId { get; set; }
    public PipelineStage Stage { get; set; }
    public string? MediaConverterJobId { get; set; }
    public int Version { get; set; }
}
