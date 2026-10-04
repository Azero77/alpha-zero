using AlphaZero.Modules.Documents.Application.Sagas;
using AlphaZero.Modules.Documents.IntegrationEvents;
using AlphaZero.Shared.Domain;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AlphaZero.Modules.Documents.UnitTests;

public class DocumentProcessingSagaTests
{
    [Fact]
    public async Task UploadInitiated_TransitionsToPending()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMassTransitTestHarness(x =>
        {
            x.AddSagaStateMachine<DocumentProcessingSaga, DocumentProcessingSagaState>();
        });

        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();

        try
        {
            var documentId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();

            await harness.Bus.Publish(new DocumentUploadInitiatedEvent(documentId, tenantId));

            Assert.True(await harness.Published.Any<DocumentUploadInitiatedEvent>());

            var sagaHarness = harness.GetSagaStateMachineHarness<DocumentProcessingSaga, DocumentProcessingSagaState>();

            Assert.True(await sagaHarness.Consumed.Any<DocumentUploadInitiatedEvent>());
            
            var instance = sagaHarness.Created.Contains(documentId);
            Assert.NotNull(instance);
            
            var sagaInstance = sagaHarness.Sagas.Contains(documentId);
            Assert.Equal("Pending", sagaInstance.CurrentState);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task UploadedToStorage_TransitionsToProcessing_AndSendsDeduplicationCommand()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMassTransitTestHarness(x =>
        {
            x.AddSagaStateMachine<DocumentProcessingSaga, DocumentProcessingSagaState>();
        });

        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();

        try
        {
            var documentId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();

            var sagaHarness = harness.GetSagaStateMachineHarness<DocumentProcessingSaga, DocumentProcessingSagaState>();

            // Setup state
            await harness.Bus.Publish(new DocumentUploadInitiatedEvent(documentId, tenantId));
            
            // Wait for it to become Pending
            await Task.Delay(100);

            await harness.Bus.Publish(new DocumentUploadedToStorageEvent(documentId, tenantId, "s3key", "hash123", 1024));

            Assert.True(await sagaHarness.Consumed.Any<DocumentUploadedToStorageEvent>());
            
            var sagaInstance = sagaHarness.Sagas.Contains(documentId);
            Assert.Equal("Processing", sagaInstance.CurrentState);

            Assert.True(await harness.Published.Any<VerifyDocumentDeduplicationCommand>());
        }
        finally
        {
            await harness.Stop();
        }
    }
}
