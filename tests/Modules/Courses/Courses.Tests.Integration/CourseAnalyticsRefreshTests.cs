using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Application.Analytics.Queries.GetCourseAnalytics;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Infrastructure.Analytics;
using AlphaZero.Modules.Courses.Presentation;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Courses.Pool;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Enroll;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Domain;
using Autofac;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Courses.Tests.Integration;

/// <summary>
/// Step 12 (issue #34 rework §3.6): Daily derived-from-source analytics refresh.
/// Deletes per-row decrements; refreshes `CourseAnalytics` in one set-based UPSERT where
/// `Status != 'Expired'` (Active, Suspended, Inactive counted; Expired excluded).
/// </summary>
public class CourseAnalyticsRefreshTests : BaseIntegrationTest
{
    public CourseAnalyticsRefreshTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedAvailableVideoAsset(Guid courseId, Guid tenantId, string title = "Test Video")
    {
        var videoId = Guid.NewGuid();
        var videoArn = ResourceArn.ForVideo(tenantId, videoId);
        var asset = VideoCourseAsset.Create(videoId, tenantId, videoArn, title, courseId).Value;
        asset.MarkAvailable();

        DbContext.CourseAssets.Add(asset);
        await DbContext.SaveChangesAsync();
        return videoId;
    }

    private async Task<Guid> CreatePublishedCourse(Guid tenantId)
    {
        SetTenant(tenantId);

        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Analytics Course", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseData!.Sections.First().Id;

        var v1 = await SeedAvailableVideoAsset(courseId, tenantId, "L1");
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v1}/assign", new AssignAssetRequest
        {
            CourseId = courseId, AssetId = v1, SectionId = sectionId, Title = "L1"
        });

        var v2 = await SeedAvailableVideoAsset(courseId, tenantId, "L2");
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v2}/assign", new AssignAssetRequest
        {
            CourseId = courseId, AssetId = v2, SectionId = sectionId, Title = "L2"
        });

        await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/approve", new { });
        await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });

        return courseId;
    }

    private async Task<Guid> EnrollAndComplete(Guid tenantId, Guid courseId, Guid studentId, params int[] bitIndexes)
    {
        SetTenant(tenantId);
        var enrollResp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        enrollResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var enrollmentId = (await enrollResp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;

        foreach (var bitIndex in bitIndexes)
        {
            var complete = await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = bitIndex });
            complete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        return enrollmentId;
    }

    [Fact]
    public async Task Refresh_Should_DeriveCorrectAnalytics_ForMultiStatusCourse_ExcludingExpired()
    {
        // Arrange: Course with 2 active items (bit 0 and bit 1)
        var tenantId = Guid.NewGuid();
        var courseId = await CreatePublishedCourse(tenantId);

        // Student 1: Active, completed item 0 (50% progress)
        var student1 = Guid.NewGuid();
        var e1Id = await EnrollAndComplete(tenantId, courseId, student1, 0);

        // Student 2: Suspended, completed items 0 and 1 (100% progress)
        var student2 = Guid.NewGuid();
        var e2Id = await EnrollAndComplete(tenantId, courseId, student2, 0, 1);
        var e2 = await DbContext.Enrollments.FirstAsync(e => e.Id == e2Id);
        e2.Suspend().IsError.Should().BeFalse();

        // Student 3: Inactive, completed item 1 (50% progress)
        var student3 = Guid.NewGuid();
        var e3Id = await EnrollAndComplete(tenantId, courseId, student3, 1);
        var e3 = await DbContext.Enrollments.FirstAsync(e => e.Id == e3Id);
        e3.Deactivate().IsError.Should().BeFalse();

        // Student 4: Expired, completed item 0 (50% progress — must be EXCLUDED from derived analytics)
        var student4 = Guid.NewGuid();
        var e4Id = await EnrollAndComplete(tenantId, courseId, student4, 0);
        var e4 = await DbContext.Enrollments.FirstAsync(e => e.Id == e4Id);
        e4.MarkExpired().IsError.Should().BeFalse();

        await DbContext.SaveChangesAsync();

        // Act: Execute set-based derived analytics refresh
        var coursesModule = Factory.Services.GetRequiredService<CoursesModule>();
        using (var moduleScope = coursesModule.CreateScope())
        {
            var refreshService = moduleScope.Resolve<ICourseAnalyticsRefreshService>();
            var affected = await refreshService.RefreshAnalyticsAsync(CancellationToken.None);
            affected.Should().BeGreaterThan(0);
        }

        // Assert: Query derived analytics via the endpoint
        SetTenant(tenantId);
        var analyticsDto = await Client.GetFromJsonAsync<CourseAnalyticsDto>($"/courses/{courseId}/analytics");
        analyticsDto.Should().NotBeNull();

        // TotalEnrollments: 3 (Students 1, 2, 3; Student 4 is Expired and excluded)
        analyticsDto!.TotalEnrollments.Should().Be(3);

        // SumOfCompletionPercentages:
        // Student 1 (50%) + Student 2 (100%) + Student 3 (50%) = 200.0%
        // Average = 200.0 / 3 = 66.67%
        analyticsDto.AverageCompletionPercentage.Should().Be(66.67);

        // ItemCompletionRates:
        // Item 0: Student 1 + Student 2 = 2 completions
        // Item 1: Student 2 + Student 3 = 2 completions
        analyticsDto.ItemCompletionRates.Should().Contain(new ItemCompletionDto(0, 2, 66.67));
        analyticsDto.ItemCompletionRates.Should().Contain(new ItemCompletionDto(1, 2, 66.67));
    }
}
