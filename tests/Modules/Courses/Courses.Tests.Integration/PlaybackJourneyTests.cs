using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Courses.Items;
using AlphaZero.Modules.Courses.Presentation.Courses.Plans.AddPlan;
using AlphaZero.Modules.Courses.Presentation.Courses.Pool;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Shared.Domain;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoAppDbContext = AlphaZero.Modules.VideoUploading.Infrastructure.Persistance.AppDbContext;

namespace Courses.Tests.Integration;

/// <summary>
/// Cross-module playback journey (issue #35):
/// published course -> curriculum item (video asset) -> playback ticket (Courses)
/// -> playback session (VideoUploading), plus the unknown-resource error paths.
/// </summary>
public class PlaybackJourneyTests : BaseIntegrationTest
{
    public PlaybackJourneyTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<(Guid CourseId, Guid SectionId, Guid ResourceId, Guid VideoId)> CreatePublishedCourseWithVideoItem(Guid tenantId)
    {
        SetTenant(tenantId);

        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "Playback" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Playback Course", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseData!.Sections.First().Id;

        var videoId = await SeedAvailableVideoAsset(courseId, tenantId, "Lesson");
        var assignResp = await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{videoId}/assign", new AssignAssetRequest
        {
            CourseId = courseId,
            AssetId = videoId,
            SectionId = sectionId,
            Title = "Lesson"
        });
        var itemId = (await assignResp.Content.ReadFromJsonAsync<AssignAssetResponse>())!.ItemId;

        // Lifecycle: draft -> review -> approved -> plan -> published
        await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/approve", new { });
        await Client.PostAsJsonAsync($"/courses/{courseId}/plans", new AddPlanRequest { Name = "Standard", PrincipalId = Guid.NewGuid() });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });

        // The playback-ticket endpoint keys on the curriculum *resource* id (route ItemId is
        // accepted by the route but the request body must carry ResourceId).
        var resourceId = await DbContext.Set<CurriculumResource>()
            .Where(r => r.CourseAssetId == videoId)
            .Select(r => r.Id)
            .SingleAsync();

        return (courseId, sectionId, resourceId, videoId);
    }

    private async Task<Guid> SeedAvailableVideoAsset(Guid courseId, Guid tenantId, string title)
    {
        var videoId = Guid.NewGuid();
        var videoArn = ResourceArn.ForVideo(tenantId, videoId);
        var asset = VideoCourseAsset.Create(videoId, tenantId, videoArn, title, courseId).Value;
        asset.MarkAvailable();

        DbContext.CourseAssets.Add(asset);
        await DbContext.SaveChangesAsync();
        return videoId;
    }

    private async Task SeedPublishedVideo(Guid tenantId, Guid videoId, string title)
    {
        SetTenant(tenantId);

        using var videoScope = Factory.Services.CreateScope();
        var videoDbContext = videoScope.ServiceProvider.GetRequiredService<VideoAppDbContext>();

        var video = Video.Create(
            videoId,
            tenantId,
            title,
            description: null,
            new VideoMetadata("lesson.mp4", "video/mp4", 1024, "h264"),
            DateTime.UtcNow).Value;
        video!.UpdateSpecifications(new VideoSpecifications(TimeSpan.FromSeconds(60), new Resolution(1920, 1080)));
        video.MarkAsPublished(DateTime.UtcNow);

        videoDbContext.Videos.Add(video);
        await videoDbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task PlaybackJourney_Should_IssueTicket_And_CreatePlaybackSession()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (courseId, sectionId, resourceId, videoId) = await CreatePublishedCourseWithVideoItem(tenantId);
        await SeedPublishedVideo(tenantId, videoId, "Lesson");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", userId.ToString());

        // Act 1: issue the policy-based playback ticket (Courses module)
        var ticketResp = await Client.PostAsJsonAsync(
            $"/courses/{courseId}/sections/{sectionId}/items/{Guid.NewGuid()}/playback-ticket",
            new IssuePlaybackTicketRequest { CourseId = courseId, ResourceId = resourceId });

        // Assert 1
        // Assert
        ticketResp.StatusCode.Should().Be(HttpStatusCode.OK, $"body: {await ticketResp.Content.ReadAsStringAsync()}");
        var ticket = (await ticketResp.Content.ReadFromJsonAsync<IssuePlaybackTicketResponse>())!;
        ticket.VideoId.Should().Be(videoId, "the ticket must carry the true external VideoId");
        ticket.Ticket.Should().NotBeNullOrWhiteSpace();
        ticket.ExpiresInSeconds.Should().BeGreaterThan(0);

        // Act 2: exchange for a playback session (VideoUploading module)
        var sessionResp = await Client.PostAsJsonAsync(
            $"/api/videos/{videoId}/playback-session",
            new { Ticket = ticket.Ticket, CourseId = courseId });

        // Assert 2
        sessionResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = (await sessionResp.Content.ReadFromJsonAsync<AlphaZero.Modules.VideoUploading.Application.Commands.CreatePlaybackSession.PlaybackSessionDto>())!;
        session.SessionId.Should().NotBe(Guid.Empty);
        session.VideoId.Should().Be(videoId);
        session.MasterManifestUrl.Should().StartWith($"/api/player/{videoId}");
        session.EdgeCapability.CookieName.Should().Be("cf_video_token");
        session.EdgeCapability.CookieValue.Should().NotBeNullOrWhiteSpace();
        session.WatermarkContext.UserId.Should().Be(userId.ToString());
        session.WatermarkContext.TenantId.Should().Be(tenantId.ToString());
    }

    [Fact]
    public async Task IssueTicket_Should_Return404_WhenCurriculumItemDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (courseId, _, _, _) = await CreatePublishedCourseWithVideoItem(tenantId);
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/courses/{courseId}/sections/{Guid.NewGuid()}/items/{Guid.NewGuid()}/playback-ticket",
            new IssuePlaybackTicketRequest { CourseId = courseId, ResourceId = Guid.NewGuid() });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, $"body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("CurriculumResource.NotFound");
    }

    [Fact]
    public async Task CreateSession_Should_Return404_WhenVideoDoesNotExist()
    {
        // Arrange
        var unknownVideoId = Guid.NewGuid();
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/videos/{unknownVideoId}/playback-session",
            new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Video.NotFound");
    }
}
