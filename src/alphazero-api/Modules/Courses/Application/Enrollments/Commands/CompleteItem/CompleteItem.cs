using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Commands.CompleteItem;

public record CompleteItemCommand(Guid EnrollmentId, int BitIndex) : ICommand<Success>;

public sealed class CompleteItemCommandHandler : IRequestHandler<CompleteItemCommand, ErrorOr<Success>>
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly ILogger<CompleteItemCommandHandler> _logger;

    public CompleteItemCommandHandler(
        IEnrollmentRepository enrollmentRepository,
        ILogger<CompleteItemCommandHandler> logger)
    {
        _enrollmentRepository = enrollmentRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(CompleteItemCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(request.EnrollmentId, cancellationToken);
        if (enrollment is null) return Error.NotFound("Enrollment.NotFound", "Enrollment not found.");

        var result = enrollment.CompleteItem(request.BitIndex);
        if (result.IsError) return result.Errors;

        _logger.LogInformation("Item at bit index {BitIndex} completed for Enrollment {EnrollmentId}.", request.BitIndex, request.EnrollmentId);
        return Result.Success;
    }
}
