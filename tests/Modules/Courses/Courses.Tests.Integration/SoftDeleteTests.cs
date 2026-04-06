using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Modules.Courses.Presentation.Courses.AddItem;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Infrastructure.Database;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Courses.Tests.Integration;

public class SoftDeleteTests : BaseIntegrationTest
{
    public SoftDeleteTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<(Guid CourseId, Guid SectionId, Guid SubjectId)> SeedCourseWithSection(Guid tenantId)
    {
        SetTenant(tenantId);
        
        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "SoftDelete Course", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseData = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseData!.Sections.First().Id;

        return (courseId, sectionId, subjectId);
    }

    [Fact]
    public async Task Section_Should_BeSoftDeleted_WhenRemovedFromDbContext()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (courseId, sectionId, _) = await SeedCourseWithSection(tenantId);

        // Act: Manually delete via DbContext to trigger interceptor
        // (Assuming for now we don't have a public endpoint for DeleteSection yet, 
        // we test the infrastructure's capability)
        await ExecuteDbContextAsync(async db => 
        {
            var section = await db.Set<AlphaZero.Modules.Courses.Domain.Aggregates.Courses.CourseSection>()
                                 .FindAsync(sectionId);
            db.Remove(section!);
            await db.SaveChangesAsync();
        });

        // Assert: Verify global filter hides it
        var courseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        courseResponse!.Sections.Should().NotContain(s => s.Id == sectionId);

        // Assert: Verify it still exists in DB with IsDeleted = true
        await ExecuteDbContextAsync(async db => 
        {
            var section = await db.Set<AlphaZero.Modules.Courses.Domain.Aggregates.Courses.CourseSection>()
                                 .IgnoreQueryFilters()
                                 .FirstOrDefaultAsync(s => s.Id == sectionId);
            
            section.Should().NotBeNull();
            section!.IsDeleted.Should().BeTrue();
            section.OnDeleted.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task Course_Should_BeSoftDeleted_AndHideAllContent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (courseId, _, _) = await SeedCourseWithSection(tenantId);

        // Act
        await ExecuteDbContextAsync(async db => 
        {
            var course = await db.Courses.FindAsync(courseId);
            db.Courses.Remove(course!);
            await db.SaveChangesAsync();
        });

        // Assert: API should return 404
        var response = await Client.GetAsync($"/courses/{courseId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Assert: Verify in DB
        await ExecuteDbContextAsync(async db => 
        {
            var course = await db.Courses
                                 .IgnoreQueryFilters()
                                 .FirstOrDefaultAsync(c => c.Id == courseId);
            
            course.Should().NotBeNull();
            course!.IsDeleted.Should().BeTrue();
        });
    }
}
