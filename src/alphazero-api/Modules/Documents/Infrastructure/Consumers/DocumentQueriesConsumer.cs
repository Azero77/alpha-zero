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
    private readonly IDocumentStorageService _storageService;

    public DocumentQueriesConsumer(
        IDocumentRepository documentRepository,
        IDocumentStorageService storageService)
    {
        _documentRepository = documentRepository;
        _storageService = storageService;
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

        var downloadUrl = await _storageService.GenerateDownloadPresignedUrlAsync(
            document.S3Key,
            $"{document.Title}.{document.FileType}",
            TimeSpan.FromHours(1));

        await context.RespondAsync(new DocumentDownloadUrlResponse(
            downloadUrl,
            $"{document.Title}.{document.FileType}",
            TimeSpan.FromHours(1)));
    }
}
