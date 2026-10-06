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
    private readonly IAuthorizationContextFactory _authorizationContextFactory;
    private readonly IPolicyEvaluatorService _policyEvaluatorService;
    private readonly IDocumentStorageService _documentStorageService;
    private readonly IDocumentRepository _documentRepository;

    public GetDocumentDownloadUrlQueryHandler(
        ITenantProvider tenantProvider,
        IAuthorizationContextFactory authorizationContextFactory,
        IPolicyEvaluatorService policyEvaluatorService,
        IDocumentStorageService documentStorageService,
        IDocumentRepository documentRepository)
    {
        _tenantProvider = tenantProvider;
        _authorizationContextFactory = authorizationContextFactory;
        _policyEvaluatorService = policyEvaluatorService;
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

        // The document's scope determines its authorization boundary.
        var scope = document.Scope.Trim('/');
        var parts = scope.Split('/');
        if (parts.Length != 2)
            return Error.Validation("Scope.Invalid", "Document scope must follow the format 'type/id'.");

        var service = parts[0].ToLowerInvariant();
        var arnResult = ResourceArn.Create(service, tenantId.Value.ToString(), scope);

        if (arnResult.IsError)
            return arnResult.Errors;

        var contextResult = await _authorizationContextFactory.Create(
            "content:read", 
            arnResult.Value, 
            AuthenticationMethod.TenantUser, 
            arnResult.Value.Value, 
            cancellationToken);

        if (contextResult.IsError)
            return contextResult.Errors;

        var authResult = await _policyEvaluatorService.Authorize(contextResult.Value);
        
        if (authResult.IsError)
        {
            return Error.Forbidden("Access.Denied", "You do not have permission to download this document.");
        }

        var downloadUrl = await _documentStorageService.GenerateDownloadPresignedUrlAsync(
            document.S3Key,
            $"{document.Title}.{document.FileType}",
            TimeSpan.FromHours(1));

        return new GetDocumentDownloadUrlResponse(downloadUrl);
    }
}
