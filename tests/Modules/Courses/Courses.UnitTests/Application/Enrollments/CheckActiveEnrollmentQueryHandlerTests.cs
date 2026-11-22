using AlphaZero.Modules.Courses.Application.Enrollments.Queries.CheckActiveEnrollment;
using AlphaZero.Modules.Courses.Application.Queries;
using FluentAssertions;
using NSubstitute;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

public sealed class CheckActiveEnrollmentQueryHandlerTests
{
    [Fact]
    public async Task Handle_uses_explicit_tenant_user_and_course_scope()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var service = Substitute.For<IEnrollmentQueryService>();
        service.HasActiveEnrollmentAsync(tenantId, userId, courseId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new CheckActiveEnrollmentQueryHandler(service);

        var result = await handler.Handle(new CheckActiveEnrollmentQuery(tenantId, userId,
            $"az:course:{tenantId:D}:course/{courseId:D}/plan/{Guid.NewGuid():D}"), CancellationToken.None);

        result.Value.Should().BeTrue();
        await service.Received(1).HasActiveEnrollmentAsync(tenantId, userId, courseId, Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_rejects_cross_tenant_resource_without_querying_enrollments()
    {
        var service = Substitute.For<IEnrollmentQueryService>();
        var handler = new CheckActiveEnrollmentQueryHandler(service);

        var result = await handler.Handle(new CheckActiveEnrollmentQuery(Guid.NewGuid(), Guid.NewGuid(),
            $"az:course:{Guid.NewGuid():D}:course/{Guid.NewGuid():D}/plan/{Guid.NewGuid():D}"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        await service.DidNotReceiveWithAnyArgs().HasActiveEnrollmentAsync(default, default, default, default);
    }
}
