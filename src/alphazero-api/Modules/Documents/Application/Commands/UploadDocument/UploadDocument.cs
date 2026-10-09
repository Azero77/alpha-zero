using AlphaZero.Modules.Documents.Application.Services;
using AlphaZero.Modules.Documents.Domain.Models;
using AlphaZero.Modules.Documents.Domain.Repositories;
using AlphaZero.Modules.Documents.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Documents.Application.Commands.UploadDocument;

public record UploadDocumentCommand(
    string Title,
    string? Description,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string? ProfileType = null,
    bool IsPublic = false
) : ICommand<UploadDocumentResponse>;

public record UploadDocumentResponse(
    Guid DocumentId,
    string Arn,
    string UploadPresignedUrl,
    string S3Key,
    Dictionary<string, string> Headers);

public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FileSizeBytes).GreaterThan(0).LessThanOrEqualTo(5L * 1024 * 1024 * 1024) // 5GB limit
            .WithMessage("File size cannot exceed 5GB.");
    }
}

public sealed class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, ErrorOr<UploadDocumentResponse>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentStorageService _storageService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<UploadDocumentCommandHandler> _logger;

    public UploadDocumentCommandHandler(
        IDocumentRepository documentRepository,
        IDocumentStorageService storageService,
        ITenantProvider tenantProvider,
        IClock clock,
        IPublishEndpoint publishEndpoint,
        ILogger<UploadDocumentCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _storageService = storageService;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<ErrorOr<UploadDocumentResponse>> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null)
            return Error.Unauthorized("Tenant.NotFound", "Active tenant is required.");

        var documentId = Guid.NewGuid();
        var extension = Path.GetExtension(request.FileName).TrimStart('.');
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = "bin";
        }

        var s3Key = DocumentFileStorageConstants.GetDocumentS3Key(tenantId.Value.ToString(), documentId.ToString(), request.FileName);

        var documentResult = Document.Create(
            documentId,
            tenantId.Value,
            request.Title,
            request.Description,
            extension,
            s3Key,
            request.FileSizeBytes,
            request.ProfileType,
            request.IsPublic,
            _clock.Now);

        if (documentResult.IsError)
            return documentResult.Errors;

        var metadata = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(request.ProfileType))
        {
            metadata["profile"] = request.ProfileType;
        }
        metadata["document-type"] = documentResult.Value.Type.ToString();

        var presignedUrl = await _storageService.GenerateUploadPresignedUrlAsync(
            s3Key,
            request.ContentType,
            TimeSpan.FromMinutes(30),
            metadata,
            request.IsPublic);

        var headersToSign = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "content-type", request.ContentType }
        };
        foreach (var pair in metadata)
        {
            headersToSign.Add($"x-amz-meta-{pair.Key.ToLowerInvariant()}", pair.Value);
        }

        _documentRepository.Add(documentResult.Value);
        
        await _publishEndpoint.Publish(new DocumentUploadInitiatedEvent(documentId, tenantId.Value), cancellationToken);

        _logger.LogInformation("Document {DocumentId} initialized for Tenant {TenantId} with ", documentId, tenantId.Value);

        return new UploadDocumentResponse(
            documentId,
            documentResult.Value.Arn.Value,
            presignedUrl,
            s3Key,
            headersToSign);
    }
}
