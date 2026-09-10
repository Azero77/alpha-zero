using AlphaZero.Modules.Documents.Domain.Repositories;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Documents.Application.Queries.ListDocuments;

public record ListDocumentsQuery(string? FileType = null) : IRequest<ErrorOr<List<DocumentSummaryResponse>>>;

public record DocumentSummaryResponse(
    Guid Id,
    string Arn,
    string Title,
    string? Description,
    string FileType,
    long FileSizeBytes,
    DateTime CreatedOn);

public sealed class ListDocumentsQueryHandler : IRequestHandler<ListDocumentsQuery, ErrorOr<List<DocumentSummaryResponse>>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ITenantProvider _tenantProvider;

    public ListDocumentsQueryHandler(
        IDocumentRepository documentRepository,
        ITenantProvider tenantProvider)
    {
        _documentRepository = documentRepository;
        _tenantProvider = tenantProvider;
    }

    public async Task<ErrorOr<List<DocumentSummaryResponse>>> Handle(ListDocumentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null)
            return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        var documents = await _documentRepository.ListAsync(request.FileType, cancellationToken);

        var result = documents
            .Select(d => new DocumentSummaryResponse(
                d.Id,
                d.Arn.Value,
                d.Title,
                d.Description,
                d.FileType,
                d.FileSizeBytes,
                d.CreatedOn))
            .ToList();

        return result;
    }
}
