using AlphaZero.Modules.Courses.Application.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;

public record SubjectDto(Guid Id, string Name, string? Description);

public record GetSubjectQuery(Guid SubjectId) : IRequest<ErrorOr<SubjectDto>>;

public sealed class GetSubjectQueryHandler : IRequestHandler<GetSubjectQuery, ErrorOr<SubjectDto>>
{
    private readonly ISubjectQueryService _subjectQueryService;

    public GetSubjectQueryHandler(ISubjectQueryService subjectQueryService)
    {
        _subjectQueryService = subjectQueryService;
    }

    public async Task<ErrorOr<SubjectDto>> Handle(GetSubjectQuery request, CancellationToken cancellationToken)
    {
        var subject = await _subjectQueryService.GetSubjectByIdAsync(request.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Error.NotFound("Subject.NotFound", $"Subject with ID {request.SubjectId} was not found.");
        }

        return subject;
    }
}
