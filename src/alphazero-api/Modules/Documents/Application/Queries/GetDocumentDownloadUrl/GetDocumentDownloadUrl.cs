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

public record GetDocumentDownloadUrlQuery(Guid DocumentId, string? VariantName = null) : IQuery<GetDocumentDownloadUrlResponse>;

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
    private readonly Aspire.Shared.AWSResources _awsResources;

    public GetDocumentDownloadUrlQueryHandler(
        ITenantProvider tenantProvider,
        IDocumentStorageService documentStorageService,
        IDocumentRepository documentRepository,
        Aspire.Shared.AWSResources awsResources)
    {
        _tenantProvider = tenantProvider;
        _documentStorageService = documentStorageService;
        _documentRepository = documentRepository;
        _awsResources = awsResources;
    }

    public async Task<ErrorOr<GetDocumentDownloadUrlResponse>> Handle(GetDocumentDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null)
            return Error.Unauthorized("Tenant.NotFound", "Active tenant is required.");

        var document = await _documentRepository.GetFirst(d => d.Id == request.DocumentId && d.TenantId == tenantId.Value, cancellationToken);
        if (document is null)
            return Error.NotFound("Document.NotFound", "Document not found.");

        var key = document.S3Key;
        var fileName = $"{document.Title}.{document.FileType}";

        if (!string.IsNullOrEmpty(request.VariantName))
        {
            key = AlphaZero.Modules.Documents.Domain.Models.Document.GetDocumentS3Key(tenantId.Value.ToString(), document.Id.ToString(), $"{request.VariantName}.webp");
            fileName = $"{document.Title}-{request.VariantName}.webp";
        }

        if (document.IsPublic && !string.IsNullOrEmpty(_awsResources.CdnDomain))
        {
            var publicUrl = $"https://{_awsResources.CdnDomain}/{key}";
            return new GetDocumentDownloadUrlResponse(publicUrl);
        }

        var downloadUrl = await _documentStorageService.GenerateDownloadPresignedUrlAsync(
            key,
            fileName,
            TimeSpan.FromHours(1));

        return new GetDocumentDownloadUrlResponse(downloadUrl);
    }
}
