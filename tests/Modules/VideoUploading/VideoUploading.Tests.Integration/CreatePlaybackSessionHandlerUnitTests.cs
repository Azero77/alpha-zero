using AlphaZero.Modules.VideoUploading.Application.Commands.CreatePlaybackSession;
using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Security;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Queries;
using AlphaZero.Shared.Security;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace VideoUploading.Tests.Integration;

public class CreatePlaybackSessionHandlerUnitTests
{
    private class FakeCurrentTenantUserRepository : ICurrentTenantUserRepository
    {
        public TenantUserDTO? UserToReturn { get; set; }

        public Task<TenantUserDTO?> GetCurrentUser() => Task.FromResult(UserToReturn);
    }

    private class FakeTenantProvider : ITenantProvider
    {
        public Guid? TenantId { get; set; }
        public Guid? GetTenant() => TenantId;
    }

    private class FakeVideoRepository : IVideoRepository
    {
        public Video? VideoToReturn { get; set; }

        public Task<Video?> GetByIdAsync(Guid id, CancellationToken token = default)
            => Task.FromResult(VideoToReturn);

        public Task<Video?> GetBySourceKeyAsync(string sourceKey, CancellationToken cancellationToken = default)
            => Task.FromResult<Video?>(null);

        public Task<PagedResult<Video>> ListAsync(int page, int perPage, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<Video>(new List<Video>(), 0, 1, 10));

        public Task AddAsync(Video video, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Add(Video entity) { }
        public void Delete(Video entity) { }
        public void Remove(Video entity) { }
        public void Update(Video entity) { }
        public Task<Video?> GetById(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(VideoToReturn);
        public Task<Video?> GetFirst(System.Linq.Expressions.Expression<Func<Video, bool>> filter, CancellationToken token = default) => Task.FromResult(VideoToReturn);
        public Task<IReadOnlyCollection<Video>> Get(System.Linq.Expressions.Expression<Func<Video, bool>> filter, CancellationToken token = default) => Task.FromResult<IReadOnlyCollection<Video>>(new List<Video>());
        public Task<IReadOnlyCollection<Video>> GetAll(CancellationToken token = default) => Task.FromResult<IReadOnlyCollection<Video>>(new List<Video>());
        public Task<bool> Any(System.Linq.Expressions.Expression<Func<Video, bool>> filter, CancellationToken token = default) => Task.FromResult(VideoToReturn != null);
        public Task<int> Count(System.Linq.Expressions.Expression<Func<Video, bool>>? filter = null, CancellationToken token = default) => Task.FromResult(VideoToReturn != null ? 1 : 0);
        public void IgnoreQueryFilter() { }
    }

    private class FakeClock : IClock
    {
        public DateTime Now => DateTime.UtcNow;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private readonly FakeCurrentTenantUserRepository _userRepo = new();
    private readonly FakeTenantProvider _tenantProvider = new();
    private readonly FakeVideoRepository _videoRepo = new();
    private readonly CloudflareCookieSigner _cookieSigner;

    public CreatePlaybackSessionHandlerUnitTests()
    {
        var settings = new CloudflareSettings
        {
            VideoHmacSecret = "unit-test-secret-key-for-playback-session-tests-min32!",
            CookieDomain = ".alphazero.academy",
            TokenTtlHours = 4.0,
            Secure = true
        };
        _cookieSigner = new CloudflareCookieSigner(Options.Create(settings));
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        _userRepo.UserToReturn = null;
        var handler = new CreatePlaybackSessionCommandHandler(
            _videoRepo,
            _cookieSigner,
            _userRepo,
            _tenantProvider,
            NullLogger<CreatePlaybackSessionCommandHandler>.Instance);

        var command = new CreatePlaybackSessionCommand(Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Identity.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenVideoDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _userRepo.UserToReturn = new TenantUserDTO(userId, "user-id", "Test Student", tenantId);
        _videoRepo.VideoToReturn = null;

        var handler = new CreatePlaybackSessionCommandHandler(
            _videoRepo,
            _cookieSigner,
            _userRepo,
            _tenantProvider,
            NullLogger<CreatePlaybackSessionCommandHandler>.Instance);

        var videoId = Guid.NewGuid();
        var command = new CreatePlaybackSessionCommand(videoId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Video.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenVideoIsNotPublished()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _userRepo.UserToReturn = new TenantUserDTO(userId, "user-id", "Test Student", tenantId);

        var clock = new FakeClock();
        var metadata = new VideoMetadata("test.mp4", "video/mp4", 1000, "ffmpeg");
        var thumbnail = ThumbnailInfo.Empty;
        // Created video defaults to Status = VideoStatus.Processing
        var video = Video.Create(Guid.NewGuid(), tenantId, "Test Title", "Desc",  metadata, thumbnail, clock).Value;
        _videoRepo.VideoToReturn = video;

        var handler = new CreatePlaybackSessionCommandHandler(
            _videoRepo,
            _cookieSigner,
            _userRepo,
            _tenantProvider,
            NullLogger<CreatePlaybackSessionCommandHandler>.Instance);

        var command = new CreatePlaybackSessionCommand(video.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Video.NotReady");
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessWithSignedCookieAndWatermark_WhenVideoIsPublished()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _userRepo.UserToReturn = new TenantUserDTO(userId, "user-id", "Ahmad Al-Khatib", tenantId, DeviceName: "+963991234567");
        _tenantProvider.TenantId = tenantId;

        var clock = new FakeClock();
        var metadata = new VideoMetadata("test.mp4", "video/mp4", 1000, "ffmpeg");
        var thumbnail = ThumbnailInfo.Empty;
        // Transition video to Published
        var video = Video.Create(Guid.NewGuid(), tenantId, "Test Title", "Desc",  metadata, thumbnail, clock).Value;
        video.MarkAsPublished("streaming/1080p", VideoSpecifications.Empty, clock);
        _videoRepo.VideoToReturn = video;

        var handler = new CreatePlaybackSessionCommandHandler(
            _videoRepo,
            _cookieSigner,
            _userRepo,
            _tenantProvider,
            NullLogger<CreatePlaybackSessionCommandHandler>.Instance);

        var command = new CreatePlaybackSessionCommand(video.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        var session = result.Value;
        session.VideoId.Should().Be(video.Id);
        session.SessionId.Should().NotBeEmpty();
        session.MasterManifestUrl.Should().Be($"/api/player/{video.Id:D}/master");

        // Edge capability cookie assertions
        session.EdgeCapability.CookieName.Should().Be("cf_video_token");
        session.EdgeCapability.CookieValue.Should().Contain(".");
        session.EdgeCapability.Path.Should().Be($"/streaming/{tenantId:D}/{video.Id:D}/");
        session.EdgeCapability.Domain.Should().Be(".alphazero.academy");
        session.EdgeCapability.HttpOnly.Should().BeTrue();
        session.EdgeCapability.Secure.Should().BeTrue();

        // Watermark identity assertions
        session.WatermarkContext.UserId.Should().Be(userId.ToString());
        session.WatermarkContext.UserName.Should().Be("Ahmad Al-Khatib");
        session.WatermarkContext.UserPhone.Should().Be("+963991234567");
        session.WatermarkContext.TenantId.Should().Be(tenantId.ToString());
    }

    [Fact]
    public void Validator_ShouldValidateVideoIdAndCourseIdRequirements()
    {
        var validator = new CreatePlaybackSessionCommandValidator();

        // Empty VideoId -> invalid
        var res1 = validator.Validate(new CreatePlaybackSessionCommand(Guid.Empty));
        res1.IsValid.Should().BeFalse();

        // ItemId provided without CourseId -> invalid
        var res2 = validator.Validate(new CreatePlaybackSessionCommand(Guid.NewGuid(), null, Guid.NewGuid()));
        res2.IsValid.Should().BeFalse();

        // Valid with VideoId
        var res3 = validator.Validate(new CreatePlaybackSessionCommand(Guid.NewGuid()));
        res3.IsValid.Should().BeTrue();

        // Valid with VideoId, CourseId, and ItemId
        var res4 = validator.Validate(new CreatePlaybackSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        res4.IsValid.Should().BeTrue();
    }
}
