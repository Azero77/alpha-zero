using AlphaZero.Modules.Documents.Application.Models;
using AlphaZero.Modules.Documents.Application.Commands.ProcessSqsMessages;
using AlphaZero.Shared.Application;
using Aspire.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AlphaZero.Modules.Documents.Infrastructure.Consumers;

public class SQSDocumentUploadedConsumer : IConsumer<DocumentUploadedQueueMessage>
{
    private readonly IDocumentsModule _module;
    private readonly ILogger<SQSDocumentUploadedConsumer> _logger;

    public SQSDocumentUploadedConsumer(IDocumentsModule module, ILogger<SQSDocumentUploadedConsumer> logger)
    {
        _module = module;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DocumentUploadedQueueMessage> context)
    {
        _logger.LogInformation("[SQS] SQSDocumentUploadedConsumer triggered for Document {DocumentId}", context.Message.DocumentId);
        await _module.Send(new ProcessDocumentUploadedCommand(context.Message), context.CancellationToken);
    }
}

public class SQSDocumentUploadedConsumerDefinition : ConsumerDefinition<SQSDocumentUploadedConsumer>
{
    public SQSDocumentUploadedConsumerDefinition(AWSResources resources)
    {
        var queueUrl = resources.DocumentUploadedQueue?.QueueUrl ?? throw new ArgumentException("DocumentUploadedQueue is not configured");
        EndpointName = queueUrl.Split('/').Last();
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<SQSDocumentUploadedConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.ConfigureConsumeTopology = false;
        endpointConfigurator.ClearSerialization();
        endpointConfigurator.UseNewtonsoftRawJsonSerializer(RawSerializerOptions.AnyMessageType);
    }
}

public class SQSDocumentProcessingCompletedConsumer : IConsumer<DocumentProcessingCompletedQueueMessage>
{
    private readonly IDocumentsModule _module;
    private readonly ILogger<SQSDocumentProcessingCompletedConsumer> _logger;

    public SQSDocumentProcessingCompletedConsumer(IDocumentsModule module, ILogger<SQSDocumentProcessingCompletedConsumer> logger)
    {
        _module = module;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DocumentProcessingCompletedQueueMessage> context)
    {
        _logger.LogInformation("[SQS] SQSDocumentProcessingCompletedConsumer triggered for Document {DocumentId}", context.Message.DocumentId);
        await _module.Send(new ProcessDocumentCompletedCommand(context.Message), context.CancellationToken);
    }
}

public class SQSDocumentProcessingCompletedConsumerDefinition : ConsumerDefinition<SQSDocumentProcessingCompletedConsumer>
{
    public SQSDocumentProcessingCompletedConsumerDefinition(AWSResources resources)
    {
        var queueUrl = resources.DocumentProcessingCompletedQueue?.QueueUrl ?? throw new ArgumentException("DocumentProcessingCompletedQueue is not configured");
        EndpointName = queueUrl.Split('/').Last();
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<SQSDocumentProcessingCompletedConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.ConfigureConsumeTopology = false;
        endpointConfigurator.ClearSerialization();
        endpointConfigurator.UseNewtonsoftRawJsonSerializer(RawSerializerOptions.AnyMessageType);
    }
}

public class SQSDocumentProcessingFailedConsumer : IConsumer<DocumentProcessingFailedQueueMessage>
{
    private readonly IDocumentsModule _module;
    private readonly ILogger<SQSDocumentProcessingFailedConsumer> _logger;

    public SQSDocumentProcessingFailedConsumer(IDocumentsModule module, ILogger<SQSDocumentProcessingFailedConsumer> logger)
    {
        _module = module;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DocumentProcessingFailedQueueMessage> context)
    {
        _logger.LogInformation("[SQS] SQSDocumentProcessingFailedConsumer triggered for Document {DocumentId}", context.Message.DocumentId);
        await _module.Send(new ProcessDocumentFailedCommand(context.Message), context.CancellationToken);
    }
}

public class SQSDocumentProcessingFailedConsumerDefinition : ConsumerDefinition<SQSDocumentProcessingFailedConsumer>
{
    public SQSDocumentProcessingFailedConsumerDefinition(AWSResources resources)
    {
        var queueUrl = resources.DocumentProcessingFailedQueue?.QueueUrl ?? throw new ArgumentException("DocumentProcessingFailedQueue is not configured");
        EndpointName = queueUrl.Split('/').Last();
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<SQSDocumentProcessingFailedConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.ConfigureConsumeTopology = false;
        endpointConfigurator.ClearSerialization();
        endpointConfigurator.UseNewtonsoftRawJsonSerializer(RawSerializerOptions.AnyMessageType);
    }
}
