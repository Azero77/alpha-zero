using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Documents.Domain.Models;

public class Document : AggregateRoot, IDomainTenantOwned, ISoftDeletable
{
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public string FileType { get; private set; } = null!; // e.g. "pdf", "docx"
    public string S3Key { get; private set; } = null!;
    public long FileSizeBytes { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? OnDeleted { get; private set; }

    private Document() { }

    private Document(
        Guid id,
        Guid tenantId,
        string title,
        string? description,
        string fileType,
        string s3Key,
        long fileSizeBytes,
        DateTime createdOn) : base(id)
    {
        TenantId = tenantId;
        Title = title;
        Description = description;
        FileType = fileType;
        S3Key = s3Key;
        FileSizeBytes = fileSizeBytes;
        CreatedOn = createdOn;
        IsDeleted = false;
    }

    public static ErrorOr<Document> Create(
        Guid id,
        Guid tenantId,
        string title,
        string? description,
        string fileType,
        string s3Key,
        long fileSizeBytes,
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("Document.Title", "Title is required.");

        if (string.IsNullOrWhiteSpace(fileType))
            return Error.Validation("Document.FileType", "File type is required.");

        return new Document(
            id,
            tenantId,
            title,
            description,
            fileType.ToLowerInvariant().TrimStart('.'),
            s3Key,
            fileSizeBytes,
            clock.Now);
    }

    public void MarkAsDeleted(IClock clock)
    {
        IsDeleted = true;
        OnDeleted = clock.Now;
    }

    public ResourceArn Arn => ResourceArn.ForDocument(TenantId, Id);
}
