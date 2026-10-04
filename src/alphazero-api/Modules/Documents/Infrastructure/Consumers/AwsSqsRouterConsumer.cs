using System.Text.Json;
using System.Text.Json.Nodes;
using AlphaZero.Modules.Documents.IntegrationEvents;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Documents.Infrastructure.Consumers;

public class AwsSqsRouterConsumer : IConsumer<JsonNode>
{
    private readonly ILogger<AwsSqsRouterConsumer> _logger;

    public AwsSqsRouterConsumer(ILogger<AwsSqsRouterConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<JsonNode> context)
    {
        var json = context.Message;
        
        // Example: S3 ObjectCreated event
        var records = json["Records"]?.AsArray();
        if (records != null && records.Count > 0)
        {
            var firstRecord = records[0];
            var eventName = firstRecord?["eventName"]?.GetValue<string>();
            if (eventName != null && eventName.StartsWith("ObjectCreated:"))
            {
                var s3 = firstRecord?["s3"];
                var objectInfo = s3?["object"];
                var key = objectInfo?["key"]?.GetValue<string>();
                var size = objectInfo?["size"]?.GetValue<long>();
                var eTag = objectInfo?["eTag"]?.GetValue<string>(); // Used as Hash
                
                if (key != null && eTag != null && size.HasValue)
                {
                    // Extract DocumentId and TenantId from Key (documents/{tenantId}/{documentId}/{filename})
                    var parts = key.Split('/');
                    if (parts.Length >= 4 && parts[0] == "documents" && Guid.TryParse(parts[1], out var tenantId) && Guid.TryParse(parts[2], out var documentId))
                    {
                        _logger.LogInformation("Routing S3 ObjectCreated event for Document {DocumentId}", documentId);
                        await context.Publish(new DocumentUploadedToStorageEvent(documentId, tenantId, key, eTag, size.Value));
                        return;
                    }
                }
            }
        }
        
        // Example: Step Functions completion payload
        var detailType = json["detail-type"]?.GetValue<string>();
        if (detailType == "Step Functions Execution Status Change")
        {
            var status = json["detail"]?["status"]?.GetValue<string>();
            var outputStr = json["detail"]?["output"]?.GetValue<string>();
            
            if (status == "SUCCEEDED" && outputStr != null)
            {
                var outputDoc = JsonNode.Parse(outputStr);
                var docIdStr = outputDoc?["DocumentId"]?.GetValue<string>();
                var tenantIdStr = outputDoc?["TenantId"]?.GetValue<string>();
                
                if (Guid.TryParse(docIdStr, out var documentId) && Guid.TryParse(tenantIdStr, out var tenantId))
                {
                    _logger.LogInformation("Routing Step Functions SUCCEEDED event for Document {DocumentId}", documentId);
                    await context.Publish(new DocumentProcessingCompletedEvent(documentId, tenantId, outputStr));
                    return;
                }
            }
        }
        
        _logger.LogWarning("Unrecognized AWS SQS payload.");
    }
}
