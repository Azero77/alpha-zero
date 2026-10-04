using AlphaZero.Modules.Documents.Domain.Models;

namespace AlphaZero.Modules.Documents.Application.Metadata;

public interface IDocumentMetadataPipeline
{
    Task<Dictionary<string, object>> ProcessAsync(DocumentType type, string payloadJson, Dictionary<string, object> initialMetadata, CancellationToken cancellationToken);
}

public class DocumentMetadataPipeline : IDocumentMetadataPipeline
{
    private readonly IEnumerable<IDocumentMetadataSource> _sources;

    public DocumentMetadataPipeline(IEnumerable<IDocumentMetadataSource> sources)
    {
        _sources = sources;
    }

    public async Task<Dictionary<string, object>> ProcessAsync(DocumentType type, string payloadJson, Dictionary<string, object> initialMetadata, CancellationToken cancellationToken)
    {
        var context = new DocumentUploadContext
        {
            Type = type,
            PayloadJson = payloadJson,
            Metadata = new Dictionary<string, object>(initialMetadata)
        };

        foreach (var source in _sources.Where(s => s.CanHandle(type)))
        {
            await source.EnhanceAsync(context, cancellationToken);
        }

        return context.Metadata;
    }
}
