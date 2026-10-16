using AlphaZero.Modules.Courses.Application.Courses.Commands.Playback;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Security;
using ErrorOr;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class IssuePlaybackTicketCommandHandlerTests
{
    private readonly ICourseRepository _courseRepository;
    private readonly IEnrollementRepository _enrollementRepository;
    private readonly IPlaybackTicketSigner _ticketSigner;
    private readonly ITenantProvider _tenantProvider;
    private readonly IssuePlaybackTicketCommandHandler _handler;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _sectionId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _videoId = Guid.NewGuid();

    public IssuePlaybackTicketCommandHandlerTests()
    {
        _courseRepository = Substitute.For<ICourseRepository>();
        _enrollementRepository = Substitute.For<IEnrollementRepository>();
        _ticketSigner = Substitute.For<IPlaybackTicketSigner>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _tenantProvider.GetTenant().Returns(_tenantId);

        _handler = new IssuePlaybackTicketCommandHandler(
            _courseRepository,
            _enrollementRepository,
            _ticketSigner,
            _tenantProvider);
    }

    private void SetupCourse(Course? course)
    {
        var builder = Substitute.For<ICourseQueryBuilder>();
        _courseRepository.Query(_courseId).Returns(builder);
        builder.WithSection(_sectionId).Returns(builder);
        builder.WithItems(Arg.Any<bool>()).Returns(builder);
        builder.WithResources(Arg.Any<bool>()).Returns(builder);
        builder.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(course);
    }

    private Course CreateValidCourseWithVideo()
    {
        var course = Course.Create(_courseId, _tenantId, "Distributed Systems", "Desc", Guid.NewGuid()).Value;
        course.AddSection(_sectionId, "Section 1");
        var section = course.Sections.First();

        var videoResource = new VideoCurriculumResource(Guid.NewGuid(), 0, _videoId, 120);
        var item = new CurriculumItem(_itemId, _sectionId, "Lesson 1", 0, 0, ResourceType.Video, new[] { videoResource });
        section.AddItem(item.Id, item.Title, item.MainResourceType, item.BitIndex, new[] { videoResource });

        return course;
    }

    [Fact]
    public async Task Handle_ShouldReturnTicket_WhenStudentEnrolledAndVideoExists()
    {
        // Arrange
        var course = CreateValidCourseWithVideo();
        SetupCourse(course);

        var enrollment = Enrollement.Create(Guid.NewGuid(), _tenantId, _studentId, _courseId, 1).Value;
        _enrollementRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<System.Func<Enrollement, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(enrollment);

        _ticketSigner.GenerateTicket(
            _tenantId, _studentId, _courseId, _itemId, _videoId, Arg.Any<TimeSpan?>(), Arg.Any<string?>())
            .Returns("valid.playback.ticket");

        var command = new IssuePlaybackTicketCommand(_courseId, _sectionId, _itemId, _studentId, "device-fp-123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Ticket.Should().Be("valid.playback.ticket");
        result.Value.VideoId.Should().Be(_videoId);
        result.Value.ExpiresInSeconds.Should().Be(120);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenStudentNotEnrolled()
    {
        // Arrange
        _enrollementRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<System.Func<Enrollement, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Enrollement?)null);

        var command = new IssuePlaybackTicketCommand(_courseId, _sectionId, _itemId, _studentId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
        result.FirstError.Code.Should().Be("Enrollment.NotEnrolled");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenCourseNotFound()
    {
        // Arrange
        var enrollment = Enrollement.Create(Guid.NewGuid(), _tenantId, _studentId, _courseId, 1).Value;
        _enrollementRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<System.Func<Enrollement, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(enrollment);

        SetupCourse(null);

        var command = new IssuePlaybackTicketCommand(_courseId, _sectionId, _itemId, _studentId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Course.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnValidation_WhenItemHasNoVideoResource()
    {
        // Arrange
        var enrollment = Enrollement.Create(Guid.NewGuid(), _tenantId, _studentId, _courseId, 1).Value;
        _enrollementRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<System.Func<Enrollement, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(enrollment);

        var course = Course.Create(_courseId, _tenantId, "Distributed Systems", "Desc", Guid.NewGuid()).Value;
        course.AddSection(_sectionId, "Section 1");
        var section = course.Sections.First();

        var docResource = DocumentCurriculumResource.Create(Guid.NewGuid(), 0, "Guide", Guid.NewGuid()).Value;
        section.AddItem(_itemId, "Doc Lesson", ResourceType.Document, 0, new[] { docResource });

        SetupCourse(course);

        var command = new IssuePlaybackTicketCommand(_courseId, _sectionId, _itemId, _studentId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be("CurriculumItem.NoVideo");
    }
}
