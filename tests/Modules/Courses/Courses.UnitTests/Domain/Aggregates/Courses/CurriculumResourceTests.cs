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
        var videoResource = new VideoCurriculumResource(id, 0, assetId, assetId, durationSeconds: 100);

        videoResource.ResourceType.Should().Be("Video");
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
            Guid.NewGuid(), 0, docId, "Cheat Sheet", "pdf", isPublic: true, variantName: "summary");

        docResource.ResourceType.Should().Be("Document");
        docResource.DocUri.Should().Be($"doc://public/{docId}?variantName=summary");

        var parsed = DocumentCurriculumResource.ParseDocUri(docResource.DocUri);
        parsed.IsPublic.Should().BeTrue();
        parsed.DocumentId.Should().Be(docId);
        parsed.VariantName.Should().Be("summary");
    }

    [Fact]
    public void ArticleCurriculumResource_Should_UpdateContent()
    {
        var resource = new ArticleCurriculumResource(Guid.NewGuid(), 0, "Initial Text");
        resource.ResourceType.Should().Be("Article");
        resource.TextContent.Should().Be("Initial Text");

        resource.UpdateContent("Updated Text");
        resource.TextContent.Should().Be("Updated Text");
    }

    [Fact]
    public void QuizCurriculumResource_Should_CheckPassingScore()
    {
        var assessmentId = Guid.NewGuid();
        var resource = new QuizCurriculumResource(Guid.NewGuid(), 0, assessmentId, passingScore: 85);

        resource.ResourceType.Should().Be("Quiz");
        resource.AssessmentId.Should().Be(assessmentId);
        resource.CanComplete(84).Should().BeFalse();
        resource.CanComplete(85).Should().BeTrue();
        resource.CanComplete(90).Should().BeTrue();
    }

    [Fact]
    public void UpdateOrder_Should_Succeed_WhenOrderIsZeroOrPositive()
    {
        var resource = new ArticleCurriculumResource(Guid.NewGuid(), 0, "Text");
        var result = resource.UpdateOrder(2);

        result.IsError.Should().BeFalse();
        resource.Order.Should().Be(2);
    }

    [Fact]
    public void UpdateOrder_Should_Fail_WhenOrderIsNegative()
    {
        var resource = new ArticleCurriculumResource(Guid.NewGuid(), 0, "Text");
        var result = resource.UpdateOrder(-1);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CurriculumResource.Order");
    }
}
