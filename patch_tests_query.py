import re

with open('tests/Modules/Documents/Documents.UnitTests/Queries/GetDocumentDownloadUrlTests.cs', 'r') as f:
    content = f.read()

# Replace _handler initialization
content = content.replace('_handler = new GetDocumentDownloadUrlQueryHandler(\n            _tenantProvider,\n            _documentStorageService,\n            _documentRepository);', 
"""_handler = new GetDocumentDownloadUrlQueryHandler(
            _tenantProvider,
            _documentStorageService,
            _documentRepository,
            new Aspire.Shared.AWSResources { CdnDomain = "public-cdn.alphazero.com" });""")

# Replace Document.Create call
content = content.replace('Document.Create(documentId, tenantId, "Title", null, "course/123", "pdf", "s3key", 1024, null, Substitute.For<IClock>())', 'Document.Create(documentId, tenantId, "Title", null, "course/123", "pdf", "s3key", 1024, null, false, Substitute.For<IClock>())')

# Add Public test
public_test = """
    [Fact]
    public async Task Handle_ReturnsDirectUrl_WhenDocumentIsPublic()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        _tenantProvider.GetTenant().Returns(tenantId);

        var document = Document.Create(documentId, tenantId, "Title", null, "course/123", "pdf", "s3key", 1024, null, true, Substitute.For<IClock>()).Value;
        
        _documentRepository.GetFirst(Arg.Any<Expression<Func<Document, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(document);

        var query = new GetDocumentDownloadUrlQuery(documentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Url.Should().Be("https://public-cdn.alphazero.com/s3key");
    }
"""

content = content.replace('    }\n}\n', '    }\n' + public_test + '}\n')

with open('tests/Modules/Documents/Documents.UnitTests/Queries/GetDocumentDownloadUrlTests.cs', 'w') as f:
    f.write(content)
