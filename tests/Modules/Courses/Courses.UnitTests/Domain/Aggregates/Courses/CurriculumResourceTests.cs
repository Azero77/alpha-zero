using System.Text.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Courses;

public class CurriculumResourceTests
{
    [Fact]
    public void VideoCurriculumResource_Should_ComputeCompletion_Correctly()
    {
        var id = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var videoResource = new VideoCurriculumResource(id, 0, assetId, durationSeconds: 100);

        videoResource.ResourceType.Should().Be(ResourceType.Video);
        videoResource.VideoId.Should().Be(assetId);
        videoResource.DurationSeconds.Should().Be(100);

        // 79 seconds < 80%
        videoResource.CanComplete(79).Should().BeFalse();
        // 80 seconds >= 80%
        videoResource.CanComplete(80).Should().BeTrue();
    }

    [Fact]
    public void DocumentCurriculumResource_Should_FormatAndParseDocUri()
    {
        var docId = Guid.NewGuid();
        var docResource = new DocumentCurriculumResource(
            Guid.NewGuid(), 0, "Cheat Sheet", docId, "pdf", isPublic: true, variantName: "summary");

        docResource.ResourceType.Should().Be(ResourceType.Document);
        docResource.DocUri.Should().Be($"doc://public/{docId}?variantName=summary");

        var parsed = DocumentCurriculumResource.ParseDocUri(docResource.DocUri);
        parsed.IsPublic.Should().BeTrue();
        parsed.DocumentId.Should().Be(docId);
        parsed.VariantName.Should().Be("summary");
    }

    [Fact]
    public void ArticleCurriculumResource_Should_UpdateContent()
    {
        var jsonContent = JsonSerializer.SerializeToElement("Initial Text");
        var resource = new ArticleCurriculumResource(Guid.NewGuid(), 0, jsonContent);
        resource.ResourceType.Should().Be(ResourceType.Article);
        resource.TextContent.Should().Be("Initial Text");

        var updatedContent = JsonSerializer.SerializeToElement("Updated Text");
        resource.UpdateContent(updatedContent);
        resource.TextContent.Should().Be("Updated Text");
    }

    [Fact]
    public void QuizCurriculumResource_Should_CheckPassingScore()
    {
        var assessmentId = Guid.NewGuid();
        var resource = new AssessmentCurriculumResource(Guid.NewGuid(), 0, passingScore: 85, courseAssetId: assessmentId);

        resource.ResourceType.Should().Be(ResourceType.Assessment);
        resource.AssessmentId.Should().Be(assessmentId);
        resource.CanComplete(84).Should().BeFalse();
        resource.CanComplete(85).Should().BeTrue();
        resource.CanComplete(90).Should().BeTrue();
    }

    [Fact]
    public void UpdateOrder_Should_Succeed_WhenOrderIsZeroOrPositive()
    {
        var jsonContent = JsonSerializer.SerializeToElement("Text");
        var resource = new ArticleCurriculumResource(Guid.NewGuid(), 0, jsonContent);
        var result = resource.UpdateOrder(2);

        result.IsError.Should().BeFalse();
        resource.Order.Should().Be(2);
    }

    [Fact]
    public void UpdateOrder_Should_Fail_WhenOrderIsNegative()
    {
        var jsonContent = JsonSerializer.SerializeToElement("Text");
        var resource = new ArticleCurriculumResource(Guid.NewGuid(), 0, jsonContent);
        var result = resource.UpdateOrder(-1);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CurriculumResource.Order");
    }
}
