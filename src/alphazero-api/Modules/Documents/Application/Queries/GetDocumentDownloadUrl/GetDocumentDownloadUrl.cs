using AlphaZero.Modules.Documents.Application.Services;
using AlphaZero.Modules.Documents.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using MediatR;
using FluentValidation;

namespace AlphaZero.Modules.Documents.Application.Queries.GetDocumentDownloadUrl;

public record GetDocumentDownloadUrlQuery(Guid DocumentId) : IQuery<GetDocumentDownloadUrlResponse>;

public record GetDocumentDownloadUrlResponse(string Url);

public class GetDocumentDownloadUrlQueryValidator : AbstractValidator<GetDocumentDownloadUrlQuery>
{
    public GetDocumentDownloadUrlQueryValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
    }
}

public class GetDocumentDownloadUrlQueryHandler : IRequestHandler<GetDocumentDownloadUrlQuery, ErrorOr<GetDocumentDownloadUrlResponse>>
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IDocumentStorageService _documentStorageService;
    private readonly IDocumentRepository _documentRepository;

    public GetDocumentDownloadUrlQueryHandler(
        ITenantProvider tenantProvider,
        IDocumentStorageService documentStorageService,
        IDocumentRepository documentRepository)
    {
        _tenantProvider = tenantProvider;
        _documentStorageService = documentStorageService;
        _documentRepository = documentRepository;
    }

    public async Task<ErrorOr<GetDocumentDownloadUrlResponse>> Handle(GetDocumentDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null)
            return Error.Unauthorized("Tenant.NotFound", "Active tenant is required.");

        var document = await _documentRepository.GetFirst(d => d.Id == request.DocumentId && d.TenantId == tenantId.Value, cancellationToken);
        if (document is null)
            return Error.NotFound("Document.NotFound", "Document not found.");

        var downloadUrl = await _documentStorageService.GenerateDownloadPresignedUrlAsync(
            document.S3Key,
            $"{document.Title}.{document.FileType}",
            TimeSpan.FromHours(1));

        return new GetDocumentDownloadUrlResponse(downloadUrl);
    }
}
