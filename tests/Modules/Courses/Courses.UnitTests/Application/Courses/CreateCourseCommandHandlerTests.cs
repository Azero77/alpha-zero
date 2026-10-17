using AlphaZero.Modules.Courses.Application.Courses.Commands.Create;
using AlphaZero.Modules.Courses.Application.EventHandlers;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class CreateCourseCommandHandlerTests
{
    private readonly ICourseRepository _courseRepository;
    private readonly ISubjectRepository _subjectRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<CreateCourseCommandHandler> _logger;
    private readonly CreateCourseCommandHandler _handler;

    public CreateCourseCommandHandlerTests()
    {
        _courseRepository = Substitute.For<ICourseRepository>();
        _subjectRepository = Substitute.For<ISubjectRepository>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _logger = Substitute.For<ILogger<CreateCourseCommandHandler>>();
        
        _handler = new CreateCourseCommandHandler(
            _courseRepository,
            _subjectRepository,
            _tenantProvider,
            _logger);
    }

    [Fact]
    public async Task Handle_Should_CreateCourseAndEmitEvent_WhenRequestIsValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var command = new CreateCourseCommand("Test Course", "Test Description", subjectId);

        _tenantProvider.GetTenant().Returns(tenantId);
        _subjectRepository.Any(Arg.Any<System.Linq.Expressions.Expression<System.Func<AlphaZero.Modules.Courses.Domain.Aggregates.Subject.Subject, bool>>>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeEmpty();
        
        _courseRepository.Received(1).Add(Arg.Is<Course>(c => 
            c.Title == "Test Course" && 
            c.TenantId == tenantId && 
            c.SubjectId == subjectId &&
            c.DomainEvents.Any(e => e is CourseCreatedDomainEvent)));
    }

    [Fact]
    public async Task CourseCreatedDomainEventHandler_Should_SeedCourseOverview()
    {
        // Arrange
        var overviewRepo = Substitute.For<ICourseOverviewRepository>();
        var logger = Substitute.For<ILogger<CourseCreatedDomainEventHandler>>();
        var eventHandler = new CourseCreatedDomainEventHandler(overviewRepo, logger);

        var courseId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var notification = new CourseCreatedDomainEvent(courseId, tenantId);

        // Act
        await eventHandler.Handle(notification, CancellationToken.None);

        // Assert
        overviewRepo.Received(1).Add(Arg.Is<CourseOverview>(o =>
            o.CourseId == courseId &&
            o.TenantId == tenantId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenTenantNotFound()
    {
        // Arrange
        _tenantProvider.GetTenant().Returns((Guid?)null);
        var command = new CreateCourseCommand("Title", "Desc", Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenSubjectDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantProvider.GetTenant().Returns(tenantId);
        _subjectRepository.Any(Arg.Any<System.Linq.Expressions.Expression<System.Func<AlphaZero.Modules.Courses.Domain.Aggregates.Subject.Subject, bool>>>())
            .Returns(false);

        var command = new CreateCourseCommand("Title", "Desc", Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.SubjectId");
    }
}
