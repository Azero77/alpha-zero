using AlphaZero.Modules.Documents.Application.Services;
using AlphaZero.Modules.Documents.Domain.Models;
using AlphaZero.Modules.Documents.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Documents.Application.Commands.UploadDocument;

public record UploadDocumentCommand(
    string Title,
    string? Description,
    string FileName,
    string ContentType,
    long FileSizeBytes
) : ICommand<UploadDocumentResponse>;

public record UploadDocumentResponse(
    Guid DocumentId,
    string Arn,
    string UploadPresignedUrl,
    string S3Key);

public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FileSizeBytes).GreaterThan(0);
    }
}

public sealed class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, ErrorOr<UploadDocumentResponse>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentStorageService _storageService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly ILogger<UploadDocumentCommandHandler> _logger;

    public UploadDocumentCommandHandler(
        IDocumentRepository documentRepository,
        IDocumentStorageService storageService,
        ITenantProvider tenantProvider,
        IClock clock,
        ILogger<UploadDocumentCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _storageService = storageService;
        _tenantProvider = tenantProvider;
        _clock = clock;
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

        var s3Key = $"documents/{tenantId.Value}/{documentId}/{request.FileName}";

        var documentResult = Document.Create(
            documentId,
            tenantId.Value,
            request.Title,
            request.Description,
            extension,
            s3Key,
            request.FileSizeBytes,
            _clock);

        if (documentResult.IsError)
            return documentResult.Errors;

        var presignedUrl = await _storageService.GenerateUploadPresignedUrlAsync(
            s3Key,
            request.ContentType,
            TimeSpan.FromMinutes(30));

        _documentRepository.Add(documentResult.Value);
        _logger.LogInformation("Document {DocumentId} initialized for Tenant {TenantId}.", documentId, tenantId.Value);

        return new UploadDocumentResponse(
            documentId,
            documentResult.Value.Arn.Value,
            presignedUrl,
            s3Key);
    }
}
