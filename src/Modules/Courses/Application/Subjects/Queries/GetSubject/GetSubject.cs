using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Subject;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;

public record GetSubjectQuery(Guid SubjectId) : IRequest<ErrorOr<Subject>>;

public sealed class GetSubjectQueryHandler : IRequestHandler<GetSubjectQuery, ErrorOr<Subject>>
{
    private readonly ISubjectRepository _subjectRepository;

    public GetSubjectQueryHandler(ISubjectRepository subjectRepository)
    {
        _subjectRepository = subjectRepository;
    }

    public async Task<ErrorOr<Subject>> Handle(GetSubjectQuery request, CancellationToken cancellationToken)
    {
        var subject = await _subjectRepository.GetFirst(s => s.Id == request.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Error.NotFound("Subject.NotFound", $"Subject with ID {request.SubjectId} was not found.");
        }

        return subject;
    }
}
