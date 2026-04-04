using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetEnrollement;

public record GetEnrollementQuery(Guid EnrollmentId) : IRequest<ErrorOr<Enrollement>>;

public sealed class GetEnrollementQueryHandler : IRequestHandler<GetEnrollementQuery, ErrorOr<Enrollement>>
{
    private readonly IEnrollementRepository _enrollementRepository;

    public GetEnrollementQueryHandler(IEnrollementRepository enrollementRepository)
    {
        _enrollementRepository = enrollementRepository;
    }

    public async Task<ErrorOr<Enrollement>> Handle(GetEnrollementQuery request, CancellationToken cancellationToken)
    {
        var enrollment = await _enrollementRepository.GetByIdAsync(request.EnrollmentId, cancellationToken);
        if (enrollment is null) return Error.NotFound("Enrollment.NotFound", "Enrollment not found.");
        return enrollment;
    }
}
