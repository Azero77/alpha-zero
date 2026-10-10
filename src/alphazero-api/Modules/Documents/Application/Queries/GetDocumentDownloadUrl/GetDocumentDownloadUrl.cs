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

public record GetDocumentDownloadUrlQuery(Guid DocumentId, string VariantName) : IQuery<GetDocumentDownloadUrlResponse>;

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
        
        var domain = document.IsPublic ? _awsResources.DocumentPublicCdnDomain : _awsResources.DocumentPrivateCdnDomain;
        if (domain is null)
            return Error.NotFound("Document.Domain.NotFound", "Domain is Not Supported For Now");
        document.GetVariantDownloadUrl(domain,request.VariantName);
        var fileName = $"{document.Title}-{request.VariantName}.webp";
        var downloadUrl = await _documentStorageService.GenerateDownloadPresignedUrlAsync(
            key,
            fileName,
            TimeSpan.FromHours(1));

        return new GetDocumentDownloadUrlResponse(downloadUrl);
    }
}
