using AlphaZero.Modules.Courses.Application.Courses.Commands.AccessTickets;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Security;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using EnrollmentAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments.Enrollment;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class IssueAccessTicketCommandHandlerTests
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAccessTicketSigner _ticketSigner;
    private readonly IOptions<AccessTicketSettings> _options;
    private readonly IAuthorizationContextProvider _authorizationContextProvider;
    private readonly IssueAccessTicketCommandHandler _handler;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();

    public IssueAccessTicketCommandHandlerTests()
    {
        _enrollmentRepository = Substitute.For<IEnrollmentRepository>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _ticketSigner = Substitute.For<IAccessTicketSigner>();
        _options = Options.Create(new AccessTicketSettings { TtlSeconds = 7200, Secret = "test-secret-at-least-32-chars-long-for-hmac!" });
        _authorizationContextProvider = Substitute.For<IAuthorizationContextProvider>();

        _tenantProvider.GetTenant().Returns(_tenantId);

        var authContext = new AuthorizationContext
        {
            Id = _userId,
            TenantId = _tenantId,
            AuthenticationMethod = "Bearer"
        };
        _authorizationContextProvider.Get(Arg.Any<CancellationToken>()).Returns(authContext);

        _handler = new IssueAccessTicketCommandHandler(
            _enrollmentRepository,
            _tenantProvider,
            _ticketSigner,
            _options,
            _authorizationContextProvider,
            Substitute.For<ILogger<IssueAccessTicketCommandHandler>>());
    }

    [Fact]
    public async Task Handle_Should_MintTicket_WhenEnrollmentActive_AndWindowValid()
    {
        var expiresOn = DateTime.UtcNow.AddDays(30);
        var enrollment = EnrollmentAggregate.Create(Guid.NewGuid(), _tenantId, _userId, _courseId, expiresOn).Value;

        _enrollmentRepository.GetByStudentAndCourseAsync(_userId, _courseId, Arg.Any<CancellationToken>())
            .Returns(enrollment);

        _ticketSigner.GenerateTicket(
            _tenantId, _userId, enrollment.Id, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan?>())
            .Returns("signed.access.ticket");

        var result = await _handler.Handle(new IssueAccessTicketCommand(_courseId, _userId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Ticket.Should().Be("signed.access.ticket");
        result.Value.ExpiresInSeconds.Should().Be(7200);
        _ticketSigner.Received(1).GenerateTicket(
            _tenantId,
            _userId,
            enrollment.Id,
            Arg.Is<DateTimeOffset>(d => d == new DateTimeOffset(enrollment.EnrolledOn, TimeSpan.Zero)),
            Arg.Is<DateTimeOffset>(d => d == new DateTimeOffset(expiresOn, TimeSpan.Zero)),
            Arg.Is<TimeSpan?>(t => t == TimeSpan.FromSeconds(7200)));
    }

    [Fact]
    public async Task Handle_Should_MintTicket_ForUnlimitedEnrollment()
    {
        var enrollment = EnrollmentAggregate.Create(Guid.NewGuid(), _tenantId, _userId, _courseId, accessExpiresOn: null).Value;

        _enrollmentRepository.GetByStudentAndCourseAsync(_userId, _courseId, Arg.Any<CancellationToken>())
            .Returns(enrollment);

        _ticketSigner.GenerateTicket(
            _tenantId, _userId, enrollment.Id, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan?>())
            .Returns("signed.unlimited.ticket");

        var result = await _handler.Handle(new IssueAccessTicketCommand(_courseId, _userId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Ticket.Should().Be("signed.unlimited.ticket");
        _ticketSigner.Received(1).GenerateTicket(
            _tenantId,
            _userId,
            enrollment.Id,
            Arg.Any<DateTimeOffset>(),
            Arg.Is<DateTimeOffset>(d => d == DateTimeOffset.MaxValue),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_Should_LazyMarkExpired_AndReturnConflict_WhenWindowElapsed()
    {
        var pastExpiresOn = DateTime.UtcNow.AddMinutes(-10);
        var enrollment = EnrollmentAggregate.Create(Guid.NewGuid(), _tenantId, _userId, _courseId, pastExpiresOn).Value;

        _enrollmentRepository.GetByStudentAndCourseAsync(_userId, _courseId, Arg.Any<CancellationToken>())
            .Returns(enrollment);

        var result = await _handler.Handle(new IssueAccessTicketCommand(_courseId, _userId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Enrollment.AccessWindowElapsed");
        enrollment.Status.Should().Be(EnrollmentStatus.Expired);
        _enrollmentRepository.Received(1).Update(enrollment);
        _ticketSigner.DidNotReceiveWithAnyArgs().GenerateTicket(default, default, default, default, default, default);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenEnrollmentAlreadyExpired()
    {
        var enrollment = EnrollmentAggregate.Create(Guid.NewGuid(), _tenantId, _userId, _courseId).Value;
        enrollment.MarkExpired().IsError.Should().BeFalse();

        _enrollmentRepository.GetByStudentAndCourseAsync(_userId, _courseId, Arg.Any<CancellationToken>())
            .Returns(enrollment);

        var result = await _handler.Handle(new IssueAccessTicketCommand(_courseId, _userId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Enrollment.Expired");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenEnrollmentSuspended()
    {
        var enrollment = EnrollmentAggregate.Create(Guid.NewGuid(), _tenantId, _userId, _courseId).Value;
        enrollment.Suspend().IsError.Should().BeFalse();

        _enrollmentRepository.GetByStudentAndCourseAsync(_userId, _courseId, Arg.Any<CancellationToken>())
            .Returns(enrollment);

        var result = await _handler.Handle(new IssueAccessTicketCommand(_courseId, _userId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Enrollment.Suspended");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenEnrollmentInactive()
    {
        var enrollment = EnrollmentAggregate.Create(Guid.NewGuid(), _tenantId, _userId, _courseId).Value;
        enrollment.Deactivate().IsError.Should().BeFalse();

        _enrollmentRepository.GetByStudentAndCourseAsync(_userId, _courseId, Arg.Any<CancellationToken>())
            .Returns(enrollment);

        var result = await _handler.Handle(new IssueAccessTicketCommand(_courseId, _userId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Enrollment.NotActive");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenEnrollmentDoesNotExist()
    {
        _enrollmentRepository.GetByStudentAndCourseAsync(_userId, _courseId, Arg.Any<CancellationToken>())
            .Returns((EnrollmentAggregate?)null);

        var result = await _handler.Handle(new IssueAccessTicketCommand(_courseId, _userId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Enrollment.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenTenantMissing()
    {
        _tenantProvider.GetTenant().Returns((Guid?)null);

        var result = await _handler.Handle(new IssueAccessTicketCommand(_courseId, _userId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Tenant.Required");
    }

    [Fact]
    public async Task Handle_Should_FallbackToAuthContext_WhenStudentIdOmitted()
    {
        var enrollment = EnrollmentAggregate.Create(Guid.NewGuid(), _tenantId, _userId, _courseId).Value;

        _enrollmentRepository.GetByStudentAndCourseAsync(_userId, _courseId, Arg.Any<CancellationToken>())
            .Returns(enrollment);

        _ticketSigner.GenerateTicket(
            _tenantId, _userId, enrollment.Id, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan?>())
            .Returns("ticket-from-auth-context");

        var result = await _handler.Handle(new IssueAccessTicketCommand(_courseId, StudentId: null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Ticket.Should().Be("ticket-from-auth-context");
        _authorizationContextProvider.Received(1).Get(Arg.Any<CancellationToken>());
    }
}
