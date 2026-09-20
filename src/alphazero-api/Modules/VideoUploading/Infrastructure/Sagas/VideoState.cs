using MassTransit;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Sagas;

public class VideoState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public Guid TenantId { get; set; }
    public string CurrentState { get; set; } = null!;
    public string? MediaConverterJobId { get; set; }
    public string? Key { get; set; }
    public int? SourceWidth { get; set; }
    public int? SourceHeight { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? S3OutputPrefix { get; set; }
    public string? FinalUrl { get; set; }
    public string? EncryptionMethod { get; set; }
    public string? CustomThumbnailKey { get; set; }
    public string? TargetResourceArn { get; set; }
    public bool IsFailed { get; set; } = false;
    public int Version { get; set; }
}
