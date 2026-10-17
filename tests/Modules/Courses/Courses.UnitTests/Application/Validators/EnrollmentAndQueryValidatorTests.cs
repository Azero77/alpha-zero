using AlphaZero.Modules.Courses.Application.Analytics.Queries.GetCourseAnalytics;
using AlphaZero.Modules.Courses.Application.Analytics.Queries.ListStudentProgress;
using AlphaZero.Modules.Courses.Application.Courses.Queries;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;
using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourses;
using AlphaZero.Modules.Courses.Application.Enrollements.Commands.CompleteItem;
using AlphaZero.Modules.Courses.Application.Enrollements.Commands.Deactivate;
using AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetEnrollement;
using AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetStudentDashboard;
using AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetStudentEnrollments;
using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using AlphaZero.Modules.Courses.Application.Subjects.Queries.ListSubjects;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Validators;

public class EnrollmentCommandValidatorTests
{
    private readonly CompleteItemCommandValidator _completeValidator = new();
    private readonly DeactivateEnrollmentCommandValidator _deactivateValidator = new();

    [Fact]
    public async Task CompleteItem_Should_Fail_WhenEnrollmentIdEmpty()
        => (await _completeValidator.ValidateAsync(new CompleteItemCommand(Guid.Empty, 0)))
            .IsValid.Should().BeFalse();

    [Theory]
    [InlineData(-1)]
    [InlineData(-42)]
    public async Task CompleteItem_Should_Fail_WhenBitIndexIsNegative(int bitIndex)
        => (await _completeValidator.ValidateAsync(new CompleteItemCommand(Guid.NewGuid(), bitIndex)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task CompleteItem_Should_Pass_WhenBitIndexIsZero()
        => (await _completeValidator.ValidateAsync(new CompleteItemCommand(Guid.NewGuid(), 0)))
            .IsValid.Should().BeTrue();

    [Fact]
    public async Task Deactivate_Should_Fail_WhenUserIdEmpty()
        => (await _deactivateValidator.ValidateAsync(new DeactivateEnrollmentCommand(Guid.Empty, Guid.NewGuid())))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Deactivate_Should_Fail_WhenCourseIdEmpty()
        => (await _deactivateValidator.ValidateAsync(new DeactivateEnrollmentCommand(Guid.NewGuid(), Guid.Empty)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Deactivate_Should_Pass_WhenValid()
        => (await _deactivateValidator.ValidateAsync(new DeactivateEnrollmentCommand(Guid.NewGuid(), Guid.NewGuid())))
            .IsValid.Should().BeTrue();
}

public class QueryValidatorTests
{
    [Fact]
    public async Task GetAllIdOnlyQueries_Should_Fail_WhenIdEmpty()
    {
        (await new GetCourseQueryValidator().ValidateAsync(new GetCourseQuery(Guid.Empty))).IsValid.Should().BeFalse();
        (await new GetCourseOverviewQueryValidator().ValidateAsync(new GetCourseOverviewQuery(Guid.Empty))).IsValid.Should().BeFalse();
        (await new GetCoursePoolQueryValidator().ValidateAsync(new GetCoursePoolQuery(Guid.Empty))).IsValid.Should().BeFalse();
        (await new GetEnrollementQueryValidator().ValidateAsync(new GetEnrollementQuery(Guid.Empty))).IsValid.Should().BeFalse();
        (await new GetStudentDashboardQueryValidator().ValidateAsync(new GetStudentDashboardQuery(Guid.Empty))).IsValid.Should().BeFalse();
        (await new GetCourseAnalyticsQueryValidator().ValidateAsync(new GetCourseAnalyticsQuery(Guid.Empty))).IsValid.Should().BeFalse();
        (await new ListStudentProgressQueryValidator().ValidateAsync(new ListStudentProgressQuery(Guid.Empty, 1, 10))).IsValid.Should().BeFalse();
        (await new GetSubjectQueryValidator().ValidateAsync(new GetSubjectQuery(Guid.Empty))).IsValid.Should().BeFalse();
        (await new GetStudentEnrollmentsQueryValidator().ValidateAsync(new GetStudentEnrollmentsQuery(Guid.Empty))).IsValid.Should().BeFalse();
        (await new GetStudentEnrollmentsForTenantQueryValidator().ValidateAsync(new GetStudentEnrollmentsForTenantQuery(Guid.NewGuid(), Guid.Empty))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllIdOnlyQueries_Should_Pass_WhenIdNotEmpty()
    {
        (await new GetCourseQueryValidator().ValidateAsync(new GetCourseQuery(Guid.NewGuid()))).IsValid.Should().BeTrue();
        (await new GetCourseOverviewQueryValidator().ValidateAsync(new GetCourseOverviewQuery(Guid.NewGuid()))).IsValid.Should().BeTrue();
        (await new GetCoursePoolQueryValidator().ValidateAsync(new GetCoursePoolQuery(Guid.NewGuid()))).IsValid.Should().BeTrue();
        (await new GetEnrollementQueryValidator().ValidateAsync(new GetEnrollementQuery(Guid.NewGuid()))).IsValid.Should().BeTrue();
        (await new GetStudentDashboardQueryValidator().ValidateAsync(new GetStudentDashboardQuery(Guid.NewGuid()))).IsValid.Should().BeTrue();
        (await new GetCourseAnalyticsQueryValidator().ValidateAsync(new GetCourseAnalyticsQuery(Guid.NewGuid()))).IsValid.Should().BeTrue();
        (await new ListStudentProgressQueryValidator().ValidateAsync(new ListStudentProgressQuery(Guid.NewGuid(), 1, 10))).IsValid.Should().BeTrue();
        (await new GetSubjectQueryValidator().ValidateAsync(new GetSubjectQuery(Guid.NewGuid()))).IsValid.Should().BeTrue();
        (await new GetStudentEnrollmentsQueryValidator().ValidateAsync(new GetStudentEnrollmentsQuery(Guid.NewGuid()))).IsValid.Should().BeTrue();
        (await new GetStudentEnrollmentsForTenantQueryValidator().ValidateAsync(new GetStudentEnrollmentsForTenantQuery(Guid.NewGuid(), Guid.NewGuid()))).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    public async Task ListCourses_Should_Fail_WhenPageIsNotPositive(int page, int perPage)
        => (await new ListCoursesQueryValidator().ValidateAsync(new ListCoursesQuery(null, page, perPage)))
            .IsValid.Should().BeFalse();

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ListCourses_Should_Fail_WhenPerPageOutOfBounds(int page, int perPage)
        => (await new ListCoursesQueryValidator().ValidateAsync(new ListCoursesQuery(null, page, perPage)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task ListCourses_Should_Pass_WhenPagingIsValid()
        => (await new ListCoursesQueryValidator().ValidateAsync(new ListCoursesQuery(null, 2, 50)))
            .IsValid.Should().BeTrue();

    [Fact]
    public async Task ListStudentProgress_Should_Fail_WhenPerPageExceedsMax()
        => (await new ListStudentProgressQueryValidator().ValidateAsync(new ListStudentProgressQuery(Guid.NewGuid(), 1, 101)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task ListSubjects_Should_Fail_WhenPerPageExceedsMax()
        => (await new ListSubjectsQueryValidator().ValidateAsync(new ListSubjectsQuery(1, 0)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task ListSubjects_Should_Pass_WhenPagingIsValid()
        => (await new ListSubjectsQueryValidator().ValidateAsync(new ListSubjectsQuery(1, 10)))
            .IsValid.Should().BeTrue();

    [Fact]
    public async Task GetStudentEnrollments_Should_Fail_WhenPerPageExceedsMax()
        => (await new GetStudentEnrollmentsQueryValidator().ValidateAsync(new GetStudentEnrollmentsQuery(Guid.NewGuid(), 1, 101)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task GetStudentEnrollmentsForTenant_Should_Fail_WhenStudentIdEmpty()
        => (await new GetStudentEnrollmentsForTenantQueryValidator().ValidateAsync(new GetStudentEnrollmentsForTenantQuery(Guid.Empty, Guid.NewGuid())))
            .IsValid.Should().BeFalse();
}
