using AlphaZero.Modules.Courses.Application.Courses.Commands.Playback;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Security;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class IssuePlaybackTicketCommandHandlerTests
{
    private readonly ICourseResourceQueryService _courseResourceQueryService;
    private readonly IPlaybackTicketSigner _ticketSigner;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAuthorizationContextProvider _authorizationContextProvider;
    private readonly IDeviceProvider _deviceProvider;
    private readonly IssuePlaybackTicketCommandHandler _handler;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _curriculumResourceId = Guid.NewGuid();

    // Layer 2 (pool asset) and Layer 1 (external video) are two DIFFERENT identities.
    private readonly Guid _courseAssetId = Guid.NewGuid();
    private readonly Guid _externalVideoId = Guid.NewGuid();

    public IssuePlaybackTicketCommandHandlerTests()
    {
        _courseResourceQueryService = Substitute.For<ICourseResourceQueryService>();
        _ticketSigner = Substitute.For<IPlaybackTicketSigner>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _authorizationContextProvider = Substitute.For<IAuthorizationContextProvider>();
        _deviceProvider = Substitute.For<IDeviceProvider>();

        _tenantProvider.GetTenant().Returns(_tenantId);
        _deviceProvider.GetDeviceId().Returns("device-fp-123");

        var authContext = new AuthorizationContext
        {
            Id = _userId,
            TenantId = _tenantId,
            AuthenticationMethod = "Bearer"
        };
        _authorizationContextProvider.Get(Arg.Any<CancellationToken>()).Returns(authContext);

        _handler = new IssuePlaybackTicketCommandHandler(
            _ticketSigner,
            _tenantProvider,
            _authorizationContextProvider,
            _deviceProvider,
            _courseResourceQueryService);
    }

    private CourseResourceDetailsDto SetupValidVideoDetails()
    {
        // Layer 2 (pool asset) and Layer 1 (external video) are two DIFFERENT identities,
        // so the ticket must carry _externalVideoId, never _courseAssetId.
        var details = new CourseResourceDetailsDto(
            _curriculumResourceId,
            _itemId,
            _courseId,
            _courseAssetId,
            _externalVideoId,
            ResourceType.Video);

        _courseResourceQueryService.GetResourceDetailsAsync(_courseId, _curriculumResourceId, Arg.Any<CancellationToken>())
            .Returns(details);

        return details;
    }

    [Fact]
    public async Task Handle_ShouldReturnTicketWithExternalVideoId_WhenResourceIsValidVideo()
    {
        // Arrange
        SetupValidVideoDetails();

        _ticketSigner.GenerateTicket(
                _tenantId, _userId, _courseId, _itemId, _externalVideoId, Arg.Any<TimeSpan?>(), Arg.Any<string?>())
            .Returns("valid.playback.ticket");

        var command = new IssuePlaybackTicketCommand(_courseId, _curriculumResourceId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Ticket.Should().Be("valid.playback.ticket");
        result.Value.VideoId.Should().Be(_externalVideoId);
        result.Value.VideoId.Should().NotBe(_courseAssetId);
        result.Value.ExpiresInSeconds.Should().Be(120);
    }

    [Fact]
    public async Task Handle_ShouldSignTicketWithTrueExternalVideoId_NotCourseAssetId()
    {
        // Arrange
        SetupValidVideoDetails();

        _ticketSigner.GenerateTicket(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<TimeSpan?>(), Arg.Any<string?>())
            .Returns("ticket");

        var command = new IssuePlaybackTicketCommand(_courseId, _curriculumResourceId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — the VideoUploading module verifies payload.VideoId == actual video id.
        _ = _ticketSigner.Received().GenerateTicket(
            _tenantId,
            _userId,
            _courseId,
            _itemId,
            _externalVideoId,
            Arg.Any<TimeSpan?>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenAuthorizationContextMissing()
    {
        // Arrange
        _authorizationContextProvider.Get(Arg.Any<CancellationToken>())
            .Returns(Error.Forbidden());

        var command = new IssuePlaybackTicketCommand(_courseId, _curriculumResourceId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenResourceNotFound()
    {
        // Arrange
        _courseResourceQueryService.GetResourceDetailsAsync(_courseId, _curriculumResourceId, Arg.Any<CancellationToken>())
            .Returns((CourseResourceDetailsDto?)null);

        var command = new IssuePlaybackTicketCommand(_courseId, _curriculumResourceId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("CurriculumResource.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnValidation_WhenResourceIsNotAVideo()
    {
        // Arrange
        var details = new CourseResourceDetailsDto(
            _curriculumResourceId, _itemId, _courseId, _courseAssetId, Guid.NewGuid(), ResourceType.Document);

        _courseResourceQueryService.GetResourceDetailsAsync(_courseId, _curriculumResourceId, Arg.Any<CancellationToken>())
            .Returns(details);
        var command = new IssuePlaybackTicketCommand(_courseId, _curriculumResourceId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be("CurriculumItem.NoVideo");
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenTenantIsMissing()
    {
        // Arrange
        SetupValidVideoDetails();

        _tenantProvider.GetTenant().Returns((Guid?)null);

        var command = new IssuePlaybackTicketCommand(_courseId, _curriculumResourceId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Tenant.Required");
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenTenantIsEmpty()
    {
        // Arrange
        SetupValidVideoDetails();

        _tenantProvider.GetTenant().Returns(Guid.Empty);

        var command = new IssuePlaybackTicketCommand(_courseId, _curriculumResourceId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Tenant.Required");
    }
}
