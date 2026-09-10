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
