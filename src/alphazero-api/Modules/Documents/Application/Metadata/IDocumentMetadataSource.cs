using AlphaZero.Modules.Documents.Domain.Models;

namespace AlphaZero.Modules.Documents.Application.Metadata;

public class DocumentUploadContext
{
    public string PayloadJson { get; set; } = string.Empty;
    public DocumentType Type { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public interface IDocumentMetadataSource
{
    bool CanHandle(DocumentType type);
    Task EnhanceAsync(DocumentUploadContext context, CancellationToken cancellationToken);
}
