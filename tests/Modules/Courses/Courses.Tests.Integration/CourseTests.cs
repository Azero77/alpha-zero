using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Courses.Plans.AddPlan;
using AlphaZero.Modules.Courses.Presentation.Courses.Pool;
using AlphaZero.Modules.Courses.Presentation.Courses.Reorder.Sections;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Domain;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests.Integration;

public class CourseTests : BaseIntegrationTest
{
    public CourseTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedSubject(Guid tenantId)
    {
        SetTenant(tenantId);
        var response = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest 
        { 
            Name = "Computer Science", 
            Description = "CS" 
        });
        var result = await response.Content.ReadFromJsonAsync<CreateSubjectResponse>();
        return result!.Id;
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

    [Fact]
    public async Task CreateCourse_Should_ReturnCreated_WhenRequestIsValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);
        var request = new CreateCourseRequest { Title = "C# Basics", Description = "Learn C#", SubjectId = subjectId };

        // Act
        var response = await Client.PostAsJsonAsync("/courses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateCourseResponse>();
        var courseId = result!.Id;

        var course = await DbContext.Courses.FindAsync(courseId);
        course.Should().NotBeNull();
        course!.Title.Should().Be("C# Basics");
        course.Status.ToString().Should().Be("Draft");
    }

    [Fact]
    public async Task CurriculumBuilding_Should_AddSectionsAndLessons()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);
        var createCourseResponse = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "C# Basics", SubjectId = subjectId });
        var courseId = (await createCourseResponse.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        // Act: Add Section
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "Introduction" });
        
        // Get course to find SectionId
        var courseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseResponse!.Sections.First().Id;

        // Seed available asset in pool
        var videoId = await SeedAvailableVideoAsset(courseId, tenantId, "Hello World");

        // Act: Assign asset to curriculum section
        var assignResponse = await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{videoId}/assign", new AssignAssetRequest 
        { 
            CourseId = courseId,
            AssetId = videoId,
            SectionId = sectionId,
            Title = "Hello World" 
        });
        assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert
        var updatedCourseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        updatedCourseResponse!.Sections.Should().HaveCount(1);
        updatedCourseResponse.Sections.First().Items.Should().HaveCount(1);
        updatedCourseResponse.Sections.First().Items.First().Title.Should().Be("Hello World");
    }

    [Fact]
    public async Task ReorderSections_Should_UpdateOrders()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);
        var createCourseResponse = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Course", SubjectId = subjectId });
        var courseId = (await createCourseResponse.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S2" });

        var courseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var s1Id = courseResponse!.Sections.First(s => s.Title == "S1").Id;
        var s2Id = courseResponse.Sections.First(s => s.Title == "S2").Id;

        // Act: Reorder (S2 first)
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections/reorder", new ReorderSectionsRequest 
        { 
            SectionIds = new List<Guid> { s2Id, s1Id } 
        });

        // Assert
        var finalCourseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        finalCourseResponse!.Sections[0].Id.Should().Be(s2Id);
        finalCourseResponse.Sections[1].Id.Should().Be(s1Id);
    }

    [Fact]
    public async Task ReorderItems_Should_UpdateOrdersWithinSection()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);
        var createCourseResponse = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Course", SubjectId = subjectId });
        var courseId = (await createCourseResponse.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseResponse!.Sections.First().Id;

        var v1Id = await SeedAvailableVideoAsset(courseId, tenantId, "L1");
        var v2Id = await SeedAvailableVideoAsset(courseId, tenantId, "L2");

        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v1Id}/assign", new AssignAssetRequest { CourseId = courseId, AssetId = v1Id, SectionId = sectionId, Title = "L1" });
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v2Id}/assign", new AssignAssetRequest { CourseId = courseId, AssetId = v2Id, SectionId = sectionId, Title = "L2" });

        var sectionResponse = (await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}"))!.Sections.First();
        var l1Id = sectionResponse.Items.First(i => i.Title == "L1").Id;
        var l2Id = sectionResponse.Items.First(i => i.Title == "L2").Id;

        // Act
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/reorder", new { ItemIds = new List<Guid> { l2Id, l1Id } });

        // Assert
        var finalSectionResponse = (await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}"))!.Sections.First();
        finalSectionResponse.Items[0].Id.Should().Be(l2Id);
        finalSectionResponse.Items[1].Id.Should().Be(l1Id);
    }

    [Fact]
    public async Task CreateCourse_Should_Fail_WhenSubjectBelongsToDifferentTenant()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        
        var subjectA = await SeedSubject(tenantA);
        
        SetTenant(tenantB);
        var request = new CreateCourseRequest { Title = "Illegal Course", SubjectId = subjectA };

        // Act
        var response = await Client.PostAsJsonAsync("/courses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RejectCourse_Should_Fail_WhenReasonIsMissing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);
        var createResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "ToReject", SubjectId = subjectId });
        var courseId = (await createResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        // Add content (required to submit)
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseResponse!.Sections.First().Id;

        var v1Id = await SeedAvailableVideoAsset(courseId, tenantId, "L1");
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v1Id}/assign", new AssignAssetRequest { CourseId = courseId, AssetId = v1Id, SectionId = sectionId, Title = "L1" });

        var submitResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        submitResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var response = await Client.PatchAsJsonAsync($"/courses/{courseId}/reject", new { Reason = "" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CourseLifecycle_Should_TransitionCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);
        var httpResponse = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "LifeCycle", SubjectId = subjectId });
        var createCourseResponse = await httpResponse.Content.ReadFromJsonAsync<CreateCourseResponse>();
        var courseId = createCourseResponse!.Id;

        // Add content (required to submit)
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseResponse!.Sections.First().Id;

        var v1Id = await SeedAvailableVideoAsset(courseId, tenantId, "L1");
        await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v1Id}/assign", new AssignAssetRequest { CourseId = courseId, AssetId = v1Id, SectionId = sectionId, Title = "L1" });

        // Act: Submit
        var submitResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/review", new { });
        submitResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act: Approve
        var approveResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/approve", new { });
        approveResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act: Add Plan (Required for publishing)
        var addPlanResp = await Client.PostAsJsonAsync($"/courses/{courseId}/plans", new AddPlanRequest 
        { 
            Name = "Standard", 
            PrincipalId = Guid.NewGuid() 
        });
        addPlanResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act: Publish
        var publishResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });
        publishResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert
        var finalCourseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        finalCourseResponse!.Status.Should().Be("Published");
    }

    [Fact]
    public async Task CoursePool_Should_SupportGetAssignAndDismiss()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);
        var createResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "PoolTest", SubjectId = subjectId });
        var courseId = (await createResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "S1" });
        var courseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseResponse!.Sections.First().Id;

        var v1Id = await SeedAvailableVideoAsset(courseId, tenantId, "Available Video 1");
        var v2Id = await SeedAvailableVideoAsset(courseId, tenantId, "Available Video 2");

        // Act 1: GET pool
        var poolResp = await Client.GetFromJsonAsync<List<AlphaZero.Modules.Courses.Application.Courses.Queries.CourseAssetDto>>($"/courses/{courseId}/pool");
        poolResp.Should().HaveCount(2);

        // Act 2: Assign v1
        var assignResp = await Client.PostAsJsonAsync($"/courses/{courseId}/pool/{v1Id}/assign", new AssignAssetRequest
        {
            CourseId = courseId,
            AssetId = v1Id,
            SectionId = sectionId,
            Title = "Assigned Item"
        });
        assignResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Pool should now only have v2
        poolResp = await Client.GetFromJsonAsync<List<AlphaZero.Modules.Courses.Application.Courses.Queries.CourseAssetDto>>($"/courses/{courseId}/pool");
        poolResp.Should().HaveCount(1);
        poolResp!.First().Id.Should().Be(v2Id);

        // Act 3: Dismiss v2
        var dismissResp = await Client.DeleteAsync($"/courses/{courseId}/pool/{v2Id}/dismiss");
        dismissResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Pool is now empty
        poolResp = await Client.GetFromJsonAsync<List<AlphaZero.Modules.Courses.Application.Courses.Queries.CourseAssetDto>>($"/courses/{courseId}/pool");
        poolResp.Should().BeEmpty();
    }
}
