using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Queries.CheckActiveEnrollment;

public sealed record CheckActiveEnrollmentQuery(Guid TenantId, Guid UserId, string TargetResourceArn)
    : IQuery<bool>;

public sealed class CheckActiveEnrollmentQueryHandler(IEnrollmentQueryService enrollments)
    : IRequestHandler<CheckActiveEnrollmentQuery, ErrorOr<bool>>
{
    public async Task<ErrorOr<bool>> Handle(CheckActiveEnrollmentQuery request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || request.UserId == Guid.Empty
            || !ResourceArn.TryParse(request.TargetResourceArn, out var arn)
            || !Guid.TryParse(arn.TenantIdString, out var arnTenantId)
            || arnTenantId != request.TenantId
            || arn.ExtractCourseId() is not { } courseId
            || courseId == Guid.Empty)
            return Error.Validation("Enrollment.TargetResource", "A tenant-matching Course resource ARN is required.");

        return await enrollments.HasActiveEnrollmentAsync(request.TenantId, request.UserId, courseId,
            DateTime.UtcNow, cancellationToken);
    }
}
