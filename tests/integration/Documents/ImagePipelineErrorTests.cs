using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using AlphaZero.Modules.Documents.Domain.Models;
using AlphaZero.Modules.Documents.Application.Models;
using AlphaZero.Shared.Domain;

namespace Documents.Tests.Integration;

public class ImagePipelineErrorTests
{
    private class FakeClock : IClock
    {
        public DateTime Now => DateTime.UtcNow;
    }

    [Fact]
    public async Task CorruptedImageUpload_ShouldEndUpInFaultedState()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var id = Guid.NewGuid();
        
        var documentResult = Document.Create(
            id,
            tenantId,
            "corrupted.jpg",
            "A corrupted image",
            "jpg",
            "uploads/corrupted.jpg",
            1024,
            new FakeClock()
        );
        
        var document = documentResult.Value;
        document.ProcessingStarted("somehash");

        // Act - Simulate what the Saga does when it receives DocumentProcessingFaultedEvent
        document.MarkAsFaulted();

        // Assert
        document.Status.Should().Be(DocumentStatus.Faulted);
        
        await Task.CompletedTask;
    }
}
