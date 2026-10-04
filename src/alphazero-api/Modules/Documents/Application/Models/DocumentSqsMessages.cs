using System;

namespace AlphaZero.Modules.Documents.Application.Models;

public record DocumentUploadedQueueMessage(
    Guid DocumentId,
    Guid TenantId,
    string S3Key,
    string FileHash,
    long Size);

public record DocumentProcessingCompletedQueueMessage(
    Guid DocumentId,
    Guid TenantId,
    string PayloadJson);

public record DocumentProcessingFailedQueueMessage(
    Guid DocumentId,
    Guid TenantId,
    string ErrorMessage);
