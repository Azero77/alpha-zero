using AlphaZero.Modules.Documents.Application.Commands.UploadDocument;
using AlphaZero.Modules.Documents.Application.Services;
using AlphaZero.Modules.Documents.Domain.Models;
using AlphaZero.Modules.Documents.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using FluentValidation.TestHelper;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Documents.UnitTests.Commands;

public class UploadDocumentValidatorTests
{
    private readonly UploadDocumentCommandValidator _validator = new();

    [Theory]
    [InlineData("course/123")]
    [InlineData("lesson/abc-def")]
    [InlineData("tenant_resources/resource-1")]
    public void Validator_ShouldNotHaveError_WhenScopeIsValid(string scope)
    {
        var model = new UploadDocumentCommand("Title", null, scope, "file.pdf", "application/pdf", 1024);
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.Scope);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalidscope")]
    [InlineData("course/123/extra")]
    [InlineData("course/../123")]
    [InlineData("/course/123")]
    public void Validator_ShouldHaveError_WhenScopeIsInvalid(string scope)
    {
        var model = new UploadDocumentCommand("Title", null, scope, "file.pdf", "application/pdf", 1024);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Scope);
    }
}

public class UploadDocumentCommandHandlerTests
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentStorageService _storageService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<UploadDocumentCommandHandler> _logger;
    private readonly UploadDocumentCommandHandler _handler;

    public UploadDocumentCommandHandlerTests()
    {
        _documentRepository = Substitute.For<IDocumentRepository>();
        _storageService = Substitute.For<IDocumentStorageService>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _clock = Substitute.For<IClock>();
        _publishEndpoint = Substitute.For<IPublishEndpoint>();
        _logger = Substitute.For<ILogger<UploadDocumentCommandHandler>>();

        _handler = new UploadDocumentCommandHandler(
            _documentRepository,
            _storageService,
            _tenantProvider,
            _clock,
            _publishEndpoint,
            _logger);
    }

    [Fact]
    public async Task Handle_GeneratesCorrectS3Key_AndUploadsPresignedUrl()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantProvider.GetTenant().Returns(tenantId);

        _storageService.GenerateUploadPresignedUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<bool>())
            .Returns("https://r2.cloudflare.com/upload-url");

        var command = new UploadDocumentCommand(
            "Test Document",
            "A test description",
            "course/123",
            "my_document.pdf",
            "application/pdf",
            2048);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        
        var expectedS3Key = $"documents/{tenantId}/course/123/{result.Value.DocumentId}/my_document.pdf";
        result.Value.S3Key.Should().Be(expectedS3Key);
        result.Value.UploadPresignedUrl.Should().Be("https://r2.cloudflare.com/upload-url");

        _documentRepository.Received(1).Add(Arg.Is<Document>(d => 
            d.Id == result.Value.DocumentId &&
            d.TenantId == tenantId &&
            d.Scope == "course/123" &&
            d.S3Key == expectedS3Key
        ));
    }
}
