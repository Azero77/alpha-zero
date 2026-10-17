using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Application.Subjects.Commands.Delete;
using AlphaZero.Modules.Courses.Application.Subjects.Commands.Update;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using SubjectAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Subject.Subject;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Subjects;

public class SubjectCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICourseRepository _courseRepository = Substitute.For<ICourseRepository>();
    private readonly ILogger<UpdateSubjectCommandHandler> _updateLogger = Substitute.For<ILogger<UpdateSubjectCommandHandler>>();
    private readonly ILogger<DeleteSubjectCommandHandler> _deleteLogger = Substitute.For<ILogger<DeleteSubjectCommandHandler>>();
    private readonly UpdateSubjectCommandHandler _updateHandler;
    private readonly DeleteSubjectCommandHandler _deleteHandler;

    public SubjectCommandHandlerTests()
    {
        _updateHandler = new UpdateSubjectCommandHandler(_subjectRepository, _updateLogger);
        _deleteHandler = new DeleteSubjectCommandHandler(_subjectRepository, _courseRepository, _deleteLogger);
    }

    [Fact]
    public async Task Update_Should_RenameSubject_AndPersist()
    {
        // Arrange
        var subject = SubjectAggregate.Create(Guid.NewGuid(), TenantId, "Old", null).Value;
        _subjectRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(subject);
        var command = new UpdateSubjectCommand(subject.Id, "New", "updated");

        // Act
        var result = await _updateHandler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        subject.Name.Should().Be("New");
        subject.Description.Should().Be("updated");
        _subjectRepository.Received(1).Update(subject);
    }

    [Fact]
    public async Task Update_Should_ReturnNotFound_WhenSubjectDoesNotExist()
    {
        // Arrange
        _subjectRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((SubjectAggregate?)null);

        // Act
        var result = await _updateHandler.Handle(new UpdateSubjectCommand(Guid.NewGuid(), "New", null), CancellationToken.None);

        // Assert
        result.FirstError.Code.Should().Be("Subject.NotFound");
    }

    [Fact]
    public async Task Delete_Should_Refuse_WhenLiveCourseReferencesSubject()
    {
        // Arrange
        var subject = SubjectAggregate.Create(Guid.NewGuid(), TenantId, "Math", null).Value;
        _subjectRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(subject);
        _courseRepository.Any(Arg.Any<System.Linq.Expressions.Expression<Func<Course, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _deleteHandler.Handle(new DeleteSubjectCommand(subject.Id), CancellationToken.None);

        // Assert: subject untouched
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Subject.InUse");
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        subject.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_Should_SoftDeleteSubject_WhenNotReferenced()
    {
        // Arrange
        var subject = SubjectAggregate.Create(Guid.NewGuid(), TenantId, "Math", null).Value;
        _subjectRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(subject);
        _courseRepository.Any(Arg.Any<System.Linq.Expressions.Expression<Func<Course, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _deleteHandler.Handle(new DeleteSubjectCommand(subject.Id), CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        subject.IsDeleted.Should().BeTrue();
        subject.OnDeleted.Should().NotBeNull();
        _subjectRepository.Received(1).Update(subject);
    }

    [Fact]
    public async Task Delete_Should_ReturnNotFound_WhenSubjectDoesNotExist()
    {
        // Arrange
        _subjectRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((SubjectAggregate?)null);

        // Act
        var result = await _deleteHandler.Handle(new DeleteSubjectCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.FirstError.Code.Should().Be("Subject.NotFound");
    }
}
