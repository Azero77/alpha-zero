using AlphaZero.Modules.VideoUploading.Application.Commands.CreatePlaybackSession;
using AlphaZero.Modules.VideoUploading.Application.Queries.GetVideoKey;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Security;
using ErrorOr;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace VideoUploading.Tests.Integration;

public class PreRequestHandlerDualAuthorizationTests
{
    private readonly IPlaybackTicketSigner _ticketSigner = Substitute.For<IPlaybackTicketSigner>();
    private readonly IPolicyEvaluatorService _policyEvaluator = Substitute.For<IPolicyEvaluatorService>();
    private readonly ITenantProvider _tenantProvider = Substitute.For<ITenantProvider>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _videoId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    public PreRequestHandlerDualAuthorizationTests()
    {
        _tenantProvider.GetTenant().Returns(_tenantId);
    }

    [Fact]
    public async Task CreatePlaybackSessionPreRequest_ShouldSucceed_WhenValidTicketProvided()
    {
        // Arrange
        var handler = new CreatePlaybackSessionPreRequestHandler(_ticketSigner, _policyEvaluator, _tenantProvider);
        var validPayload = new PlaybackTicketPayload(_tenantId, _userId, _courseId, _itemId, _videoId, DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds());
        string? error = null;

        _ticketSigner.TryVerifyTicket("valid.ticket", out Arg.Any<PlaybackTicketPayload?>(), out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = validPayload;
                x[2] = error;
                return true;
            });

        var query = new CreatePlaybackSessionQuery(_videoId, "valid.ticket", _courseId, _itemId);

        // Act
        var result = await handler.PreHandle(query, null, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        await _policyEvaluator.DidNotReceiveWithAnyArgs().Authorize(Arg.Any<AuthorizationContext>());
    }

    [Fact]
    public async Task CreatePlaybackSessionPreRequest_ShouldFail_WhenTicketIsInvalidOrExpired()
    {
        // Arrange
        var handler = new CreatePlaybackSessionPreRequestHandler(_ticketSigner, _policyEvaluator, _tenantProvider);
        string? error = "Signature mismatch.";

        _ticketSigner.TryVerifyTicket("bad.ticket", out Arg.Any<PlaybackTicketPayload?>(), out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = null;
                x[2] = error;
                return false;
            });

        var query = new CreatePlaybackSessionQuery(_videoId, "bad.ticket");

        // Act
        var result = await handler.PreHandle(query, null, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Ticket.InvalidOrExpired");
    }

    [Fact]
    public async Task CreatePlaybackSessionPreRequest_ShouldFail_WhenTicketVideoIdMismatched()
    {
        // Arrange
        var handler = new CreatePlaybackSessionPreRequestHandler(_ticketSigner, _policyEvaluator, _tenantProvider);
        var otherVideoId = Guid.NewGuid();
        var payload = new PlaybackTicketPayload(_tenantId, _userId, _courseId, _itemId, otherVideoId, DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds());

        _ticketSigner.TryVerifyTicket("ticket.other.video", out Arg.Any<PlaybackTicketPayload?>(), out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = payload;
                x[2] = null;
                return true;
            });

        var query = new CreatePlaybackSessionQuery(_videoId, "ticket.other.video");

        // Act
        var result = await handler.PreHandle(query, null, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Ticket.VideoMismatch");
    }

    [Fact]
    public async Task CreatePlaybackSessionPreRequest_ShouldFallbackToIam_WhenTicketOmitted()
    {
        // Arrange
        var handler = new CreatePlaybackSessionPreRequestHandler(_ticketSigner, _policyEvaluator, _tenantProvider);
        var authContext = new AuthorizationContext
        {
            Id = _userId,
            TenantId = _tenantId,
            AuthenticationMethod = AuthenticationMethod.TenantUser.ToString()
        };

        _policyEvaluator.Authorize(Arg.Any<AuthorizationContext>())
            .Returns(Result.Success);

        var query = new CreatePlaybackSessionQuery(_videoId, Ticket: null, CourseId: _courseId);

        // Act
        var result = await handler.PreHandle(query, authContext, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        await _policyEvaluator.Received(1).Authorize(Arg.Is<AuthorizationContext>(c =>
            c.RequiredPermission == "video:Stream"));
    }

    [Fact]
    public async Task GetVideoKeyPreRequest_ShouldSucceed_WhenValidTicketProvided()
    {
        // Arrange
        var handler = new GetVideoKeyPreRequestHandler(_ticketSigner, _policyEvaluator, _tenantProvider);
        var validPayload = new PlaybackTicketPayload(_tenantId, _userId, _courseId, _itemId, _videoId, DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds());

        _ticketSigner.TryVerifyTicket("valid.ticket", out Arg.Any<PlaybackTicketPayload?>(), out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = validPayload;
                x[2] = null;
                return true;
            });

        var query = new GetVideoKeyQuery(_videoId, "valid.ticket");

        // Act
        var result = await handler.PreHandle(query, null, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        await _policyEvaluator.DidNotReceiveWithAnyArgs().Authorize(Arg.Any<AuthorizationContext>());
    }

    [Fact]
    public async Task GetVideoKeyPreRequest_ShouldFallbackToIam_WhenTicketOmitted()
    {
        // Arrange
        var handler = new GetVideoKeyPreRequestHandler(_ticketSigner, _policyEvaluator, _tenantProvider);
        var authContext = new AuthorizationContext
        {
            Id = _userId,
            TenantId = _tenantId,
            AuthenticationMethod = AuthenticationMethod.TenantUser.ToString()
        };

        _policyEvaluator.Authorize(Arg.Any<AuthorizationContext>())
            .Returns(Result.Success);

        var query = new GetVideoKeyQuery(_videoId, Ticket: null);

        // Act
        var result = await handler.PreHandle(query, authContext, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        await _policyEvaluator.Received(1).Authorize(Arg.Is<AuthorizationContext>(c =>
            c.RequiredPermission == "video:Stream"));
    }
}
