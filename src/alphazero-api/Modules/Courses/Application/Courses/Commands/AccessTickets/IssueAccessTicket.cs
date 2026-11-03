using AlphaZero.Modules.Courses.Application.Enrollments;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Security;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.AccessTickets;

public record IssueAccessTicketCommand(
    Guid CourseId,
    Guid? StudentId = null) : ICommand<IssueAccessTicketResult>;

public record IssueAccessTicketResult(
    string Ticket,
    int ExpiresInSeconds);

public class IssueAccessTicketCommandValidator : AbstractValidator<IssueAccessTicketCommand>
{
    public IssueAccessTicketCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
    }
}

public sealed class IssueAccessTicketCommandHandler : IRequestHandler<IssueAccessTicketCommand, ErrorOr<IssueAccessTicketResult>>
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAccessTicketSigner _ticketSigner;
    private readonly IOptions<AccessTicketSettings> _options;
    private readonly IAuthorizationContextProvider _authorizationContextProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IssueAccessTicketCommandHandler>? _logger;

    public IssueAccessTicketCommandHandler(
        IEnrollmentRepository enrollmentRepository,
        ITenantProvider tenantProvider,
        IAccessTicketSigner ticketSigner,
        IOptions<AccessTicketSettings> options,
        IAuthorizationContextProvider authorizationContextProvider,
        IUnitOfWork unitOfWork,
        ILogger<IssueAccessTicketCommandHandler>? logger = null)
    {
        _enrollmentRepository = enrollmentRepository;
        _tenantProvider = tenantProvider;
        _ticketSigner = ticketSigner;
        _options = options;
        _authorizationContextProvider = authorizationContextProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ErrorOr<IssueAccessTicketResult>> Handle(
        IssueAccessTicketCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (!tenantId.HasValue || tenantId == Guid.Empty)
        {
            return Error.Conflict("Tenant.Required", "Access tickets cannot be issued globally.");
        }

        Guid studentId;
        if (request.StudentId.HasValue && request.StudentId.Value != Guid.Empty)
        {
            studentId = request.StudentId.Value;
        }
        else
        {
            var authContext = _authorizationContextProvider.Get();
            if (authContext.IsError || authContext.Value is null)
                return Error.Forbidden();

            studentId = authContext.Value.Id;
        }

        var enrollment = await _enrollmentRepository.GetByStudentAndCourseAsync(
            studentId, request.CourseId, cancellationToken);

        if (enrollment is null)
        {
            return Error.NotFound("Enrollment.NotFound",
                $"No enrollment found for student {studentId} in course {request.CourseId}.");
        }

        var now = DateTime.UtcNow;
        if (enrollment.AccessExpiresOn.HasValue && now >= enrollment.AccessExpiresOn.Value)
        {
            // Path b lazy expiry (issue #34 rework §3.4): stateful mint detects an elapsed
            // window, marks the enrollment Expired via the aggregate guard, and fails with 409.
            if (enrollment.Status == EnrollmentStatus.Active)
            {
                var (markResult, _) = _enrollmentRepository.Transitioned(
                    enrollment, () => enrollment.MarkExpired());

                if (markResult.IsError)
                {
                    return markResult.Errors;
                }

                // Path b lazy expiry must persist DESPITE this request returning 409: the shared
                // UnitOfWork decorator only saves when the handler result is non-error, so commit
                // the Expired transition (and its staged audit row) explicitly here (§3.4).
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Error.Conflict("Enrollment.AccessWindowElapsed",
                "The access window for this enrollment has elapsed.");
        }

        if (enrollment.Status == EnrollmentStatus.Suspended)
        {
            return Error.Conflict("Enrollment.Suspended", "Enrollment is suspended.");
        }

        if (enrollment.Status == EnrollmentStatus.Expired)
        {
            return Error.Conflict("Enrollment.Expired", "Enrollment has expired.");
        }

        if (enrollment.Status != EnrollmentStatus.Active)
        {
            return Error.Conflict("Enrollment.NotActive",
                $"Enrollment is in state '{enrollment.Status}' and cannot receive an access ticket.");
        }

        var windowStart = new DateTimeOffset(enrollment.EnrolledOn, TimeSpan.Zero);
        var windowEnd = enrollment.AccessExpiresOn.HasValue
            ? new DateTimeOffset(enrollment.AccessExpiresOn.Value, TimeSpan.Zero)
            : DateTimeOffset.MaxValue;

        var ttlSeconds = _options.Value.TtlSeconds > 0 ? _options.Value.TtlSeconds : 7200;
        var ttl = TimeSpan.FromSeconds(ttlSeconds);

        var ticket = _ticketSigner.GenerateTicket(
            tenantId.Value,
            studentId,
            enrollment.Id,
            windowStart,
            windowEnd,
            ttl);

        _logger?.LogInformation(
            "Issued access ticket for Student {StudentId} in Course {CourseId}, Enrollment {EnrollmentId}.",
            studentId, request.CourseId, enrollment.Id);

        return new IssueAccessTicketResult(ticket, ttlSeconds);
    }
}
