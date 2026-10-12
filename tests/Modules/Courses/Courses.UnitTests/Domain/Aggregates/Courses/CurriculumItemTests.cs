using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Courses;

public class CurriculumItemTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid CourseId = Guid.NewGuid();

    [Fact]
    public void AddResource_Should_AddResourceToCollection()
    {
        var item = new CurriculumItem(Guid.NewGuid(), TenantId, SectionId, "Lesson 1", 0, 0, "Video");
        var videoId = Guid.NewGuid();
        var resource = new VideoCurriculumResource(Guid.NewGuid(), 0, videoId, videoId, 120);

        item.AddResource(resource);

        item.Resources.Should().HaveCount(1);
        item.Resources.First().CourseAssetId.Should().Be(videoId);
        item.Resources.First().CurriculumItemId.Should().Be(item.Id);
    }

    [Fact]
    public void RemoveResource_Should_RemoveResourceAndReorderRemaining()
    {
        var item = new CurriculumItem(Guid.NewGuid(), TenantId, SectionId, "Lesson 1", 0, 0, "Video");
        var r1 = new VideoCurriculumResource(Guid.NewGuid(), 0, Guid.NewGuid(), Guid.NewGuid(), 60);
        var r2 = new ArticleCurriculumResource(Guid.NewGuid(), 1, "Notes");

        item.AddResource(r1);
        item.AddResource(r2);

        item.RemoveResource(r1.Id);

        item.Resources.Should().HaveCount(1);
        item.Resources.First().Id.Should().Be(r2.Id);
        item.Resources.First().Order.Should().Be(0);
    }

    [Fact]
    public void ReorderResources_Should_ReorderResourcesByAssetOrResourceId()
    {
        var item = new CurriculumItem(Guid.NewGuid(), TenantId, SectionId, "Lesson 1", 0, 0, "Video");

        var v1Id = Guid.NewGuid();
        var v2Id = Guid.NewGuid();
        var r1 = new VideoCurriculumResource(Guid.NewGuid(), 0, v1Id, v1Id);
        var r2 = new VideoCurriculumResource(Guid.NewGuid(), 1, v2Id, v2Id);

        item.AddResource(r1);
        item.AddResource(r2);

        item.Resources.First().CourseAssetId.Should().Be(v1Id);

        // Reorder v2 first
        item.ReorderResources(new List<Guid> { v2Id, v1Id });

        var resources = item.Resources.ToList();
        resources[0].CourseAssetId.Should().Be(v2Id);
        resources[0].Order.Should().Be(0);
        resources[1].CourseAssetId.Should().Be(v1Id);
        resources[1].Order.Should().Be(1);
    }

    [Fact]
    public void Delete_And_Restore_Should_ManageLifecycle()
    {
        var item = new CurriculumItem(Guid.NewGuid(), TenantId, SectionId, "Lesson 1", 0, 0, "Video");
        item.IsDeleted.Should().BeFalse();

        var delResult = item.Delete();
        delResult.IsError.Should().BeFalse();
        item.IsDeleted.Should().BeTrue();
        item.OnDeleted.Should().NotBeNull();

        // Double delete fails
        item.Delete().IsError.Should().BeTrue();

        var restResult = item.Restore();
        restResult.IsError.Should().BeFalse();
        item.IsDeleted.Should().BeFalse();
        item.OnDeleted.Should().BeNull();
    }

    [Theory]
    [InlineData("Video")]
    [InlineData("Document")]
    [InlineData("Article")]
    [InlineData("Quiz")]
    [InlineData("video")] // Case-insensitive
    public void Create_Should_Succeed_For_ValidMainTypes(string mainType)
    {
        var result = CurriculumItem.Create(Guid.NewGuid(), TenantId, SectionId, "Lesson 1", 0, 5, mainType);

        result.IsError.Should().BeFalse();
        result.Value.BitIndex.Should().Be(5);
        result.Value.MainType.Should().BeEquivalentTo(mainType);
    }

    [Fact]
    public void Create_Should_Fail_When_MainTypeIsInvalid()
    {
        var result = CurriculumItem.Create(Guid.NewGuid(), TenantId, SectionId, "Lesson 1", 0, 0, "InvalidType");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CurriculumItem.MainType");
    }

    [Fact]
    public void Create_Should_Fail_When_TitleIsEmpty()
    {
        var result = CurriculumItem.Create(Guid.NewGuid(), TenantId, SectionId, "  ", 0, 0, "Video");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CurriculumItem.Title");
    }

    [Fact]
    public void Update_Should_UpdateFields_And_ValidateMainType()
    {
        var item = CurriculumItem.Create(Guid.NewGuid(), TenantId, SectionId, "Lesson 1", 0, 0, "Video").Value;

        var newResource = new DocumentCurriculumResource(Guid.NewGuid(), 0, Guid.NewGuid(), "Cheatsheet");
        var updateResult = item.Update("Updated Lesson", "Document", new[] { newResource });

        updateResult.IsError.Should().BeFalse();
        item.Title.Should().Be("Updated Lesson");
        item.MainType.Should().Be("Document");
        item.Resources.Should().HaveCount(1);
    }

    [Fact]
    public void Update_Should_Fail_When_MainTypeIsInvalid()
    {
        var item = CurriculumItem.Create(Guid.NewGuid(), TenantId, SectionId, "Lesson 1", 0, 0, "Video").Value;

        var result = item.Update("Updated", "RandomType");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CurriculumItem.MainType");
    }

    [Fact]
    public void Delete_Should_RetainBitIndex_And_MarkDeleted()
    {
        var item = CurriculumItem.Create(Guid.NewGuid(), TenantId, SectionId, "Lesson 1", 0, 7, "Video").Value;

        item.Delete();

        item.IsDeleted.Should().BeTrue();
        item.BitIndex.Should().Be(7); // BitIndex burned permanently
    }

    [Fact]
    public void DocumentCurriculumResource_DocUri_GeneratesAndParsesCorrectly()
    {
        var docId = Guid.NewGuid();
        var docResource = new DocumentCurriculumResource(
            Guid.NewGuid(), 0, docId, "Cheat Sheet", "pdf", isPublic: true, variantName: "thumb");

        docResource.DocUri.Should().Be($"doc://public/{docId}?variantName=thumb");

        var parsed = DocumentCurriculumResource.ParseDocUri(docResource.DocUri);
        parsed.IsPublic.Should().BeTrue();
        parsed.DocumentId.Should().Be(docId);
        parsed.VariantName.Should().Be("thumb");
    }
}
