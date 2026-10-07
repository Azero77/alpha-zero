using AlphaZero.Shared.Domain;
using ErrorOr;
using AlphaZero.Modules.Documents.Domain.Events;
using System.Text.Json;

namespace AlphaZero.Modules.Documents.Domain.Models;

public class Document : AggregateRoot, IDomainTenantOwned, ISoftDeletable
{
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public string FileType { get; private set; } = null!; // e.g. "pdf", "docx"
    public string S3Key { get; private set; } = null!;
    public long FileSizeBytes { get; private set; }
    
    public string? FileHash { get; private set; }
    public bool IsPublic { get; private set; }
    public DocumentType Type { get; private set; }
    public DocumentStatus Status { get; private set; }
    public Dictionary<string, object> Metadata { get; private set; } = new();

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
        DocumentType type,
        bool isPublic,
        DateTime createdOn) : base(id)
    {
        TenantId = tenantId;
        Title = title;
        Description = description;
        FileType = fileType;
        S3Key = s3Key;
        FileSizeBytes = fileSizeBytes;
        Type = type;
        IsPublic = isPublic;
        Status = DocumentStatus.Pending;
        CreatedOn = createdOn;
        IsDeleted = false;
        Metadata = new Dictionary<string, object>();
    }

    public static ErrorOr<Document> Create(
        Guid id,
        Guid tenantId,
        string title,
        string? description,
        string fileType,
        string s3Key,
        long fileSizeBytes,
        string? profileType,
        bool isPublic,
        DateTime createdOn)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("Document.Title", "Title is required.");
        if (string.IsNullOrWhiteSpace(fileType))
            return Error.Validation("Document.FileType", "File type is required.");

        var ext = fileType.ToLowerInvariant().TrimStart('.');
        var type = ext switch
        {
            "jpg" or "jpeg" or "png" or "gif" or "webp" => DocumentType.Image,
            "mp4" or "mkv" or "avi" or "mov" => DocumentType.Video,
            "mp3" or "wav" or "ogg" => DocumentType.Audio,
            "pdf" => DocumentType.Pdf,
            "doc" or "docx" or "txt" => DocumentType.Document,
            _ => DocumentType.Unknown
        };

        var doc = new Document(
            id,
            tenantId,
            title,
            description,
            ext,
            s3Key,
            fileSizeBytes,
            type,
            isPublic,
            createdOn);
            
        if (!string.IsNullOrEmpty(profileType))
        {
            doc.Metadata["ProcessingProfile"] = profileType;
        }
        
        return doc;
    }

    public void MarkAsDeleted(IClock clock)
    {
        IsDeleted = true;
        OnDeleted = clock.Now;
    }

    public ErrorOr<Success> UpdateInformation(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("Document.Title", "Title is required.");

        Title = title;
        Description = description;

        AddDomainEvent(new DocumentMetadataUpdatedDomainEvent(Id, Title, Description));

        return Result.Success;
    }

    public void LinkToExistingBlob(string hash, string s3Key)
    {
        FileHash = hash;
        S3Key = s3Key;
        Status = DocumentStatus.Ready;
    }

    public void ProcessingStarted(string hash)
    {
        FileHash = hash;
        Status = DocumentStatus.Processing;
    }

    public ErrorOr<Success> ProcessingCompleted(Dictionary<string, object> metadata, DateTime occuredOn)
    {
        foreach (var kvp in metadata)
        {
            Metadata[kvp.Key] = kvp.Value;
        }
        Status = DocumentStatus.Ready;
        AddDomainEvent(new DocumentProcessingCompletedEvent(Guid.NewGuid(),Id, occuredOn));
        return Result.Success;
    }

    public ErrorOr<Success> ProcessingCompleted(string metadataAsJson, DateTime occuredOn)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataAsJson);
        if(dict is null)
            return Error.Validation("Document.Metadata", "The string is serializable to json, revise it please ");
        return ProcessingCompleted(dict, occuredOn);
    }

    public void MarkAsFaulted()
    {
        Status = DocumentStatus.Faulted;
    }

    public ResourceArn Arn => ResourceArn.ForDocument(TenantId, Id);
}


public static class DocumentFileStorageConstants
{
    public static string GetDocumentS3Key(string tenantId, string documentId, string fileName)
    {
        return $"documents/{tenantId}/{documentId}/{fileName.TrimStart('/')}";
    }

    public static string GetDocumentS3Url(string bucketName, string tenantId,  string documentId, string fileName)
    {
        return $"s3://{bucketName}/{GetDocumentS3Key(tenantId, documentId, fileName)}";
    }
}
public record DocumentProcessingCompletedEvent(Guid Id,Guid DocumentId, DateTime OccuredOn) : IDomainEvent;