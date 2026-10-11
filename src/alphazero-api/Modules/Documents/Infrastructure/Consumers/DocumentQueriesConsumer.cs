using AlphaZero.Modules.Documents.Application.Services;
using AlphaZero.Modules.Documents.Domain.Repositories;
using AlphaZero.Modules.Documents.IntegrationEvents;
using MassTransit;

namespace AlphaZero.Modules.Documents.Infrastructure.Consumers;

public class DocumentQueriesConsumer : 
    IConsumer<DocumentExistsQuery>,
    IConsumer<GetDocumentDownloadUrlQuery>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentStorageService _privateStorageService;
    private readonly IDocumentStorageService _publicStorageService;

    public DocumentQueriesConsumer(
        IDocumentRepository documentRepository,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices(AlphaZero.Modules.Documents.Application.Models.StorageType.InputPrivate)] IDocumentStorageService privateStorageService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices(AlphaZero.Modules.Documents.Application.Models.StorageType.InputPublic)] IDocumentStorageService publicStorageService)
    {
        _documentRepository = documentRepository;
        _privateStorageService = privateStorageService;
        _publicStorageService = publicStorageService;
    }

    public async Task Consume(ConsumeContext<DocumentExistsQuery> context)
    {
        var document = await _documentRepository.GetFirst(
            d => d.Id == context.Message.DocumentId && d.TenantId == context.Message.TenantId);

        if (document is null)
        {
            await context.RespondAsync(new DocumentNotFoundResponse(context.Message.DocumentId));
            return;
        }

        await context.RespondAsync(new DocumentExistsResponse(
            true,
            document.Title,
            document.FileType,
            document.FileSizeBytes));
    }

    public async Task Consume(ConsumeContext<GetDocumentDownloadUrlQuery> context)
    {
        var document = await _documentRepository.GetFirst(
            d => d.Id == context.Message.DocumentId && d.TenantId == context.Message.TenantId);

        if (document is null)
        {
            await context.RespondAsync(new DocumentNotFoundResponse(context.Message.DocumentId));
            return;
        }

        var storageService = document.IsPublic ? _publicStorageService : _privateStorageService;

        var downloadUrl = await storageService.GenerateDownloadPresignedUrlAsync(
            document.S3Key,
            $"{document.Title}.{document.FileType}",
            TimeSpan.FromHours(1));

        await context.RespondAsync(new DocumentDownloadUrlResponse(
            downloadUrl,
            $"{document.Title}.{document.FileType}",
            TimeSpan.FromHours(1)));
    }
}
