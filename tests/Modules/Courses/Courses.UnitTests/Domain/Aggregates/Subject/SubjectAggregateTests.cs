using AlphaZero.Shared.Domain;
using ErrorOr;
using FluentAssertions;
using Xunit;
using SubjectAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Subject.Subject;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Subject;

public class SubjectAggregateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static SubjectAggregate CreateSubject(string name = "Mathematics", string? description = null)
    {
        return SubjectAggregate.Create(Guid.NewGuid(), TenantId, name, description).Value;
    }

    [Fact]
    public void Update_Should_RenameSubject()
    {
        // Arrange
        var subject = CreateSubject("Mathematics", "old description");

        // Act
        var result = subject.Update("Applied Mathematics", "new description");

        // Assert
        result.IsError.Should().BeFalse();
        subject.Name.Should().Be("Applied Mathematics");
        subject.Description.Should().Be("new description");
    }

    [Fact]
    public void Update_Should_Fail_WhenNameIsEmpty()
    {
        // Arrange
        var subject = CreateSubject();

        // Act
        var result = subject.Update("", null);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        subject.Name.Should().Be("Mathematics");
    }

    [Fact]
    public void Delete_Should_MarkSubjectAsSoftDeleted_Once()
    {
        // Arrange
        var subject = CreateSubject();

        // Act
        var result = subject.Delete();

        // Assert
        result.IsError.Should().BeFalse();
        subject.IsDeleted.Should().BeTrue();
        subject.OnDeleted.Should().NotBeNull();

        // Act: second delete is refused
        var second = subject.Delete();

        second.IsError.Should().BeTrue();
        second.FirstError.Type.Should().Be(ErrorType.Conflict);
        subject.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Restore_Should_UnmarkDeletedSubject()
    {
        // Arrange
        var subject = CreateSubject();
        subject.Delete();

        // Act
        var result = subject.Restore();

        // Assert
        result.IsError.Should().BeFalse();
        subject.IsDeleted.Should().BeFalse();
        subject.OnDeleted.Should().BeNull();

        // Act: restore of a live subject is refused
        var secondRestore = subject.Restore();

        secondRestore.IsError.Should().BeTrue();
    }
}
