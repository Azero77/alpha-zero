using MediatR;

namespace AlphaZero.Modules.Documents.IntegrationEvents;

public record DocumentExistsQuery(Guid DocumentId, Guid TenantId) : IRequest<DocumentExistsResponse>;

public record DocumentExistsResponse(
    bool Exists,
    string? Title = null,
    string? FileType = null,
    long FileSizeBytes = 0);

public record DocumentNotFoundResponse(Guid DocumentId);

public record GetDocumentDownloadUrlQuery(Guid DocumentId, Guid TenantId) : IRequest<DocumentDownloadUrlResponse>;

public record DocumentDownloadUrlResponse(
    string DownloadUrl,
    string FileName,
    TimeSpan ExpiresIn);

/// <summary>
/// Fact: Document metadata has changed.
/// Used by Courses module to sync its materialized view (Read Model).
/// </summary>
public record DocumentMetadataChangedIntegrationEvent(
    Guid DocumentId,
    string Title,
    string? Description,
    string FileType,
    long FileSizeBytes);
