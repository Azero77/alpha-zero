using System;

namespace AlphaZero.Modules.Documents.Application.Models;

public record SQSDocumentUploadedEvent(
    Guid DocumentId,
    Guid TenantId,
    string S3Key,
    string FileHash,
    long Size);

public record SQSDocumentProcessingCompletedEvent(
    Guid DocumentId,
    Guid TenantId,
    string PayloadJson);

public record SQSDocumentProcessingFailed(
    Guid DocumentId,
    Guid TenantId,
    string ErrorMessage);
