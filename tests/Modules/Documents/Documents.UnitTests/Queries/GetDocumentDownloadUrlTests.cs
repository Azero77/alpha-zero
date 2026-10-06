using AlphaZero.Modules.Documents.Application.Queries.GetDocumentDownloadUrl;
using AlphaZero.Modules.Documents.Application.Services;
using AlphaZero.Modules.Documents.Domain.Models;
using AlphaZero.Modules.Documents.Domain.Repositories;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using Xunit;

namespace Documents.UnitTests.Queries;

public class GetDocumentDownloadUrlTests
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IAuthorizationContextFactory _authorizationContextFactory;
    private readonly IPolicyEvaluatorService _policyEvaluatorService;
    private readonly IDocumentStorageService _documentStorageService;
    private readonly IDocumentRepository _documentRepository;
    private readonly GetDocumentDownloadUrlQueryHandler _handler;

    public GetDocumentDownloadUrlTests()
    {
        _tenantProvider = Substitute.For<ITenantProvider>();
        _authorizationContextFactory = Substitute.For<IAuthorizationContextFactory>();
        _policyEvaluatorService = Substitute.For<IPolicyEvaluatorService>();
        _documentStorageService = Substitute.For<IDocumentStorageService>();
        _documentRepository = Substitute.For<IDocumentRepository>();

        _handler = new GetDocumentDownloadUrlQueryHandler(
            _tenantProvider,
            _authorizationContextFactory,
            _policyEvaluatorService,
            _documentStorageService,
            _documentRepository);
    }

    [Fact]
    public async Task Handle_Unauthorized_WhenTenantIsNull()
    {
        // Arrange
        _tenantProvider.GetTenant().Returns((Guid?)null);
        var query = new GetDocumentDownloadUrlQuery(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_NotFound_WhenDocumentDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantProvider.GetTenant().Returns(tenantId);
        _documentRepository.GetFirst(Arg.Any<Expression<Func<Document, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Document?)null);
        
        var query = new GetDocumentDownloadUrlQuery(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_Forbidden_WhenNotAuthorized()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        _tenantProvider.GetTenant().Returns(tenantId);

        var document = Document.Create(documentId, tenantId, "Title", null, "course/123", "pdf", "s3key", 1024, null, Substitute.For<IClock>()).Value;
        
        _documentRepository.GetFirst(Arg.Any<Expression<Func<Document, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(document);

        var authContextResult = ErrorOrFactory.From(default(AuthorizationContext)!);
        _authorizationContextFactory.Create(Arg.Any<string>(), Arg.Any<ResourceArn>(), Arg.Any<AuthenticationMethod>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(authContextResult);

        _policyEvaluatorService.Authorize(Arg.Any<AuthorizationContext>())
            .Returns(Error.Forbidden("Access.Denied", "Denied"));

        var query = new GetDocumentDownloadUrlQuery(documentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_ReturnsPresignedUrl_WhenAuthorized()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        _tenantProvider.GetTenant().Returns(tenantId);

        var document = Document.Create(documentId, tenantId, "Title", null, "course/123", "pdf", "s3key", 1024, null, Substitute.For<IClock>()).Value;
        
        _documentRepository.GetFirst(Arg.Any<Expression<Func<Document, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(document);

        var authContextResult = ErrorOrFactory.From(default(AuthorizationContext)!);
        _authorizationContextFactory.Create(Arg.Any<string>(), Arg.Any<ResourceArn>(), Arg.Any<AuthenticationMethod>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(authContextResult);

        _policyEvaluatorService.Authorize(Arg.Any<AuthorizationContext>())
            .Returns(Result.Success);

        _documentStorageService.GenerateDownloadPresignedUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>())
            .Returns("https://r2.cloudflare.com/s3key?signature=abc");

        var query = new GetDocumentDownloadUrlQuery(documentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Url.Should().Be("https://r2.cloudflare.com/s3key?signature=abc");
    }
}
