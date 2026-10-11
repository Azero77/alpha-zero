using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Items;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Presentation.Courses.AddSection;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Courses.Items;
using AlphaZero.Modules.Courses.Presentation.Courses.Overview;
using AlphaZero.Modules.Courses.Presentation.Courses.Plans.AddPlan;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Domain;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Courses.Tests.Integration;

public class CourseBuilderAndLifecycleIntegrationTests : BaseIntegrationTest
{
    public CourseBuilderAndLifecycleIntegrationTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedSubject(Guid tenantId)
    {
        SetTenant(tenantId);
        var response = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest
        {
            Name = "Physics",
            Description = "Classical Physics"
        });
        var result = await response.Content.ReadFromJsonAsync<CreateSubjectResponse>();
        return result!.Id;
    }

    private async Task<Guid> SeedAvailableVideoAsset(Guid courseId, Guid tenantId, string title = "Kinematics Lecture")
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
    public async Task CreateCourse_Should_AutoSeedCourseOverview_Preventing404RaceCondition()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);

        // Act
        var createResponse = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest
        {
            Title = "Quantum Computing",
            Description = "Foundations of QC",
            SubjectId = subjectId
        });

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateCourseResponse>();
        var courseId = createResult!.Id;

        // Immediately fetch overview - MUST NOT 404
        var overviewResponse = await Client.GetAsync($"/courses/{courseId}/overview");
        overviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var overviewDto = await overviewResponse.Content.ReadFromJsonAsync<CourseOverviewResponse>();
        overviewDto.Should().NotBeNull();
        overviewDto!.CourseId.Should().Be(courseId);

        // Database record check
        var dbOverview = await DbContext.CourseOverviews.FirstOrDefaultAsync(o => o.CourseId == courseId);
        dbOverview.Should().NotBeNull();
        dbOverview!.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task CompositeCurriculumItem_CRUD_And_AssetRefCountLifecycle_WorksCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);

        var createCourseResponse = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest
        {
            Title = "Classical Mechanics",
            SubjectId = subjectId
        });
        var courseId = (await createCourseResponse.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        // Add section
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "Unit 1: Kinematics" });
        var courseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseResponse!.Sections.First().Id;

        // Seed available asset in pool (RefCount = 0, Available)
        var assetId = await SeedAvailableVideoAsset(courseId, tenantId, "Motion in 1D");

        // 1. CREATE Item 1 referencing assetId
        var compositeContent1 = JsonDocument.Parse($@"[
            {{
                ""type"": ""video"",
                ""courseAssetId"": ""{assetId}"",
                ""videoId"": ""{Guid.NewGuid()}""
            }},
            {{
                ""type"": ""inline"",
                ""tiptap"": {{ ""type"": ""doc"", ""content"": [] }}
            }}
        ]").RootElement;

        var createItem1Request = new CreateCourseSectionItemRequest
        {
            CourseId = courseId,
            SectionId = sectionId,
            Title = "Lesson 1: Introduction",
            MainType = "Video",
            Content = compositeContent1
        };

        var item1Response = await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/items", createItem1Request);
        item1Response.StatusCode.Should().Be(HttpStatusCode.Created);

        var item1Dto = await item1Response.Content.ReadFromJsonAsync<CurriculumItemDto>();
        item1Dto.Should().NotBeNull();
        item1Dto!.BitIndex.Should().Be(0);
        item1Dto.MainType.Should().Be("Video");

        // Verify Asset RefCount incremented to 1 and State == InUse
        var assetInDb = await DbContext.CourseAssets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assetId);
        assetInDb.Should().NotBeNull();
        assetInDb!.RefCount.Should().Be(1);
        assetInDb.State.Should().Be(CourseAssetState.InUse);

        // 2. CREATE Item 2 without asset -> BitIndex should be 1
        var compositeContent2 = JsonDocument.Parse(@"[
            {
                ""type"": ""inline"",
                ""tiptap"": { ""type"": ""doc"", ""content"": [] }
            }
        ]").RootElement;

        var createItem2Request = new CreateCourseSectionItemRequest
        {
            CourseId = courseId,
            SectionId = sectionId,
            Title = "Lesson 2: Reading",
            MainType = "Article",
            Content = compositeContent2
        };

        var item2Response = await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/items", createItem2Request);
        item2Response.StatusCode.Should().Be(HttpStatusCode.Created);
        var item2Dto = await item2Response.Content.ReadFromJsonAsync<CurriculumItemDto>();
        item2Dto!.BitIndex.Should().Be(1);

        // 3. UPDATE Item 1 to remove asset reference -> RefCount drops to 0, returns to Available, BitIndex unchanged
        var updatedContent1 = JsonDocument.Parse(@"[
            {
                ""type"": ""inline"",
                ""tiptap"": { ""type"": ""doc"", ""content"": [{ ""type"": ""text"", ""text"": ""Updated content"" }] }
            }
        ]").RootElement;

        var updateItem1Request = new UpdateCourseSectionItemRequest
        {
            CourseId = courseId,
            SectionId = sectionId,
            ItemId = item1Dto.Id,
            Title = "Lesson 1: Text Only",
            MainType = "Article",
            Content = updatedContent1
        };

        var updateResponse = await Client.PutAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/items/{item1Dto.Id}", updateItem1Request);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedItem1Dto = await updateResponse.Content.ReadFromJsonAsync<CurriculumItemDto>();
        updatedItem1Dto!.BitIndex.Should().Be(0); // BitIndex strictly immutable

        assetInDb = await DbContext.CourseAssets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assetId);
        assetInDb!.RefCount.Should().Be(0);
        assetInDb.State.Should().Be(CourseAssetState.Available);

        // 4. DELETE Item 2 -> BitIndex 1 burned
        var deleteResponse = await Client.DeleteAsync($"/courses/{courseId}/sections/{sectionId}/items/{item2Dto.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 5. CREATE Item 3 -> BitIndex MUST be 2 (never reuses burned BitIndex 1)
        var createItem3Request = new CreateCourseSectionItemRequest
        {
            CourseId = courseId,
            SectionId = sectionId,
            Title = "Lesson 3: Lab",
            MainType = "Document",
            Content = compositeContent2
        };

        var item3Response = await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/items", createItem3Request);
        item3Response.StatusCode.Should().Be(HttpStatusCode.Created);
        var item3Dto = await item3Response.Content.ReadFromJsonAsync<CurriculumItemDto>();
        item3Dto!.BitIndex.Should().Be(2);
    }

    [Fact]
    public async Task GetCourseReadiness_Should_ReturnDetailedChecklistAndBlockingReasons()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);

        var createCourseResponse = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest
        {
            Title = "Thermodynamics",
            SubjectId = subjectId
        });
        var courseId = (await createCourseResponse.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        // Act: Evaluate readiness of new empty course
        var readinessResponse = await Client.GetAsync($"/courses/{courseId}/readiness");

        // Assert
        readinessResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var readiness = await readinessResponse.Content.ReadFromJsonAsync<CourseReadiness>();
        readiness.Should().NotBeNull();
        readiness!.CanPublish.Should().BeFalse();
        readiness.CanSubmitForReview.Should().BeFalse();
        readiness.Checks.Overview.HasTitle.Should().BeTrue();
        readiness.Checks.Overview.HasSubject.Should().BeTrue();
        readiness.Checks.Curriculum.HasSections.Should().BeFalse();
        readiness.Checks.Curriculum.HasItems.Should().BeFalse();
        readiness.Checks.Commercial.HasActivePricingPlan.Should().BeFalse();
        readiness.BlockingReasons.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FastPathPublish_DirectFromDraft_Should_Return422WhenUnready_And_SucceedWhenReady()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = await SeedSubject(tenantId);
        SetTenant(tenantId);

        var createCourseResponse = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest
        {
            Title = "Electromagnetism",
            Description = "Maxwell's Equations",
            SubjectId = subjectId
        });
        var courseId = (await createCourseResponse.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;

        // 1. Direct publish from Draft without required sections/plans/cover -> MUST fail with 422
        var earlyPublishResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });
        earlyPublishResp.StatusCode.Should().Be((HttpStatusCode)422);

        // 2. Satisfy all readiness criteria
        // Add section
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new AddSectionRequest { Title = "Unit 1" });
        var courseResponse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        var sectionId = courseResponse!.Sections.First().Id;

        // Add section item
        var itemContent = JsonDocument.Parse(@"[{""type"":""inline"",""tiptap"":{""type"":""doc""}}]").RootElement;
        await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{sectionId}/items", new CreateCourseSectionItemRequest
        {
            CourseId = courseId,
            SectionId = sectionId,
            Title = "Gauss's Law",
            MainType = "Article",
            Content = itemContent
        });

        // Add active pricing plan
        await Client.PostAsJsonAsync($"/courses/{courseId}/plans", new AddPlanRequest
        {
            CourseId = courseId,
            Name = "Full Access",
            PrincipalId = Guid.NewGuid()
        });

        // Set cover image on overview
        await ExecuteDbContextAsync(async db =>
        {
            var overview = await db.CourseOverviews.FirstOrDefaultAsync(o => o.CourseId == courseId);
            overview?.Update(
                AlphaZero.Modules.Courses.Domain.ValueObjects.RichText.Create("{\"type\":\"doc\",\"content\":[]}").Value,
                null,
                null,
                Guid.NewGuid());
            await db.SaveChangesAsync();
        });

        // 3. Direct publish from Draft now that all criteria are satisfied -> MUST succeed with 204
        var publishResp = await Client.PatchAsJsonAsync($"/courses/{courseId}/publish", new { });
        publishResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify status is Published
        var finalCourse = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        finalCourse!.Status.Should().Be("Published");
    }
}
