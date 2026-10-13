using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Services;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Courses;

public class CourseAggregateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();

    private static Course CreateTestCourse()
    {
        return Course.Create(Guid.NewGuid(), TenantId, "Math 101", "Basics", SubjectId).Value;
    }

    [Fact]
    public void AddSection_Should_AppendSectionWithCorrectOrder()
    {
        var course = CreateTestCourse();
        var s1Result = course.AddSection("Section 1");
        var s2Result = course.AddSection("Section 2");

        s1Result.IsError.Should().BeFalse();
        s2Result.IsError.Should().BeFalse();
        course.Sections.Should().HaveCount(2);
        course.Sections.ElementAt(0).Title.Should().Be("Section 1");
        course.Sections.ElementAt(0).Order.Should().Be(0);
        course.Sections.ElementAt(1).Title.Should().Be("Section 2");
        course.Sections.ElementAt(1).Order.Should().Be(1);
    }

    [Fact]
    public void AddPlan_And_RemovePlan_Should_ManagePricingPlans()
    {
        var course = CreateTestCourse();
        var principalId = Guid.NewGuid();
        var planResult = course.AddPlan("Standard", principalId);

        planResult.IsError.Should().BeFalse();
        course.Plans.Should().HaveCount(1);
        course.Plans.First().Name.Should().Be("Standard");

        var removeResult = course.RemovePlan(planResult.Value.Id);
        removeResult.IsError.Should().BeFalse();
        course.Plans.Should().BeEmpty();
    }

    [Fact]
    public void Section_AddItem_Should_AssignOrder_AndStoreInItems()
    {
        var course = CreateTestCourse();
        course.AddSection("Mechanics");
        var section = course.Sections.First();

        var vId = Guid.NewGuid();
        var videoResource = new VideoCurriculumResource(Guid.NewGuid(), 0, vId, 120);

        var bitIndex = course.AllocateNextBitIndex();
        var result = section.AddItem(Guid.NewGuid(), "Kinematics Lesson", "Video", bitIndex, new[] { videoResource });

        result.IsError.Should().BeFalse();
        var item = result.Value;
        item.BitIndex.Should().Be(0);
        item.Order.Should().Be(0);
        section.Items.Should().HaveCount(1);
    }

    [Fact]
    public void Section_UpdateItem_Should_UpdateItemFields()
    {
        var course = CreateTestCourse();
        course.AddSection("Mechanics");
        var section = course.Sections.First();

        var bitIndex = course.AllocateNextBitIndex();
        var item = section.AddItem(Guid.NewGuid(), "Lesson 1", "Video", bitIndex).Value;

        var jsonContent = System.Text.Json.JsonSerializer.SerializeToElement("Updated Content");
        var newResource = new ArticleCurriculumResource(Guid.NewGuid(), 0, jsonContent);
        var updateResult = section.UpdateItem(item.Id, "Updated Lesson 1", "Article", new[] { newResource });

        updateResult.IsError.Should().BeFalse();
        item.Title.Should().Be("Updated Lesson 1");
        item.MainType.Should().Be("Article");
        item.Resources.Should().HaveCount(1);
    }

    [Fact]
    public void Section_RemoveItem_Should_SoftDeleteItem()
    {
        var course = CreateTestCourse();
        course.AddSection("Mechanics");
        var section = course.Sections.First();

        var bitIndex = course.AllocateNextBitIndex();
        var item = section.AddItem(Guid.NewGuid(), "Lesson 1", "Video", bitIndex).Value;

        var removeResult = section.RemoveItem(item.Id);

        removeResult.IsError.Should().BeFalse();
        item.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void CoursePublishingService_DirectPublish_Should_Succeed_WhenCanDirectPublishAndReady()
    {
        var course = Course.Create(Guid.NewGuid(), TenantId, "Quantum Computing", "Advanced quantum concepts", SubjectId).Value;
        course.AddSection("Foundations");
        var section = course.Sections.First();

        var vId = Guid.NewGuid();
        var asset = VideoCourseAsset.Create(vId, TenantId, ResourceArn.ForVideo(TenantId, vId), "Qubit Basics", course.Id).Value;
        asset.MarkAvailable();

        var resource = new VideoCurriculumResource(Guid.NewGuid(), 0, vId, 100);
        section.AddItem(Guid.NewGuid(), "Qubits", "Video", course.AllocateNextBitIndex(), new[] { resource });
        course.AddPlan("Pro", Guid.NewGuid());

        var overview = CourseOverview.Create(
            Guid.NewGuid(), TenantId, course.Id,
            AlphaZero.Modules.Courses.Domain.ValueObjects.RichText.Create("{\"type\":\"doc\"}").Value,
            null, null, Guid.NewGuid()); // Has cover image

        var service = new CoursePublishingService(new CourseReadinessService());

        // Act
        var result = service.Publish(course, overview, canDirectPublish: true, assets: new[] { asset });

        // Assert
        result.IsError.Should().BeFalse();
        course.Status.Should().Be(CourseStatus.Published);
    }

    [Fact]
    public void CoursePublishingService_DirectPublish_Should_Fail_WhenOverviewMissingCover()
    {
        var course = Course.Create(Guid.NewGuid(), TenantId, "Quantum Computing", "Advanced quantum concepts", SubjectId).Value;
        course.AddSection("Foundations");
        var section = course.Sections.First();

        var vId = Guid.NewGuid();
        var asset = VideoCourseAsset.Create(vId, TenantId, ResourceArn.ForVideo(TenantId, vId), "Qubit Basics", course.Id).Value;
        asset.MarkAvailable();

        var resource = new VideoCurriculumResource(Guid.NewGuid(), 0, vId, 100);
        section.AddItem(Guid.NewGuid(), "Qubits", "Video", course.AllocateNextBitIndex(), new[] { resource });
        course.AddPlan("Pro", Guid.NewGuid());

        var overviewNoCover = CourseOverview.Create(
            Guid.NewGuid(), TenantId, course.Id,
            AlphaZero.Modules.Courses.Domain.ValueObjects.RichText.Create("{\"type\":\"doc\"}").Value,
            null, null, null); // Missing cover image

        var service = new CoursePublishingService(new CourseReadinessService());

        var result = service.Publish(course, overviewNoCover, canDirectPublish: true, assets: new[] { asset });

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.NotReady");
    }

    [Fact]
    public void CoursePublishingService_DirectPublish_Should_FailWithConflict_WhenNotAuthorized()
    {
        var course = Course.Create(Guid.NewGuid(), TenantId, "Quantum Computing", "Description", SubjectId).Value;
        course.AddSection("Foundations");
        course.AddPlan("Standard", Guid.NewGuid());

        var service = new CoursePublishingService(new CourseReadinessService());

        var result = service.Publish(course, overview: null, assets: null, canDirectPublish: false);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Status");
    }
}
