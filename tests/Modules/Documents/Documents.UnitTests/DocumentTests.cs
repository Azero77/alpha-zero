using AlphaZero.Modules.Documents.Domain.Models;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Documents.UnitTests;

public class DocumentTests
{
    private readonly IClock _clock;
    private readonly DateTime _now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    public DocumentTests()
    {
        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(_now);
    }

    [Fact]
    public void Create_Should_Succeed_WhenInputIsValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var result = Document.Create(
            id,
            tenantId,
            "Course Syllabus",
            "Full syllabus for 2026",
            "pdf",
            "documents/tenant/id/syllabus.pdf",
            1024 * 1024,
            _clock);

        // Assert
        result.IsError.Should().BeFalse();
        var doc = result.Value;
        doc.Id.Should().Be(id);
        doc.TenantId.Should().Be(tenantId);
        doc.Title.Should().Be("Course Syllabus");
        doc.Description.Should().Be("Full syllabus for 2026");
        doc.FileType.Should().Be("pdf");
        doc.S3Key.Should().Be("documents/tenant/id/syllabus.pdf");
        doc.FileSizeBytes.Should().Be(1024 * 1024);
        doc.CreatedOn.Should().Be(_now);
        doc.IsDeleted.Should().BeFalse();
        doc.OnDeleted.Should().BeNull();
        doc.Arn.Value.Should().Be($"az:document:{tenantId}:document/{id}");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_Should_Fail_WhenTitleIsEmpty(string? title)
    {
        // Arrange
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var result = Document.Create(
            id,
            tenantId,
            title!,
            null,
            "pdf",
            "s3/key",
            100,
            _clock);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Document.Title");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_Should_Fail_WhenFileTypeIsEmpty(string? fileType)
    {
        // Arrange
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var result = Document.Create(
            id,
            tenantId,
            "Valid Title",
            null,
            fileType!,
            "s3/key",
            100,
            _clock);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Document.FileType");
    }

    [Fact]
    public void MarkAsDeleted_Should_SetIsDeletedAndOnDeleted()
    {
        // Arrange
        var doc = Document.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Title",
            null,
            "pdf",
            "s3/key",
            100,
            _clock).Value;

        var deleteTime = _now.AddDays(1);
        _clock.Now.Returns(deleteTime);

        // Act
        doc.MarkAsDeleted(_clock);

        // Assert
        doc.IsDeleted.Should().BeTrue();
        doc.OnDeleted.Should().Be(deleteTime);
    }

    [Fact]
    public void FileType_Should_BeNormalized_WhenDotPrefixProvided()
    {
        // Act
        var result = Document.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test",
            null,
            ".PDF",
            "s3/key",
            500,
            _clock);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.FileType.Should().Be("pdf");
    }
}
