using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using Microsoft.EntityFrameworkCore;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Subjects.Commands.Delete;

public record DeleteSubjectCommand(Guid SubjectId) : ICommand<Success>;

public class DeleteSubjectCommandValidator : AbstractValidator<DeleteSubjectCommand>
{
    public DeleteSubjectCommandValidator()
    {
        RuleFor(x => x.SubjectId).NotEmpty();
    }
}

public sealed class DeleteSubjectCommandHandler(
    ISubjectRepository subjectRepository,
    ICourseRepository courseRepository,
    ILogger<DeleteSubjectCommandHandler> logger) : IRequestHandler<DeleteSubjectCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeleteSubjectCommand request, CancellationToken cancellationToken)
    {
        // Tracked load: this handler mutates the loaded aggregate.
        var subject = await subjectRepository.GetByIdAsync(request.SubjectId, cancellationToken);
        if (subject is null) return Error.NotFound("Subject.NotFound", "Subject not found.");

        // A live course (global query filter already excludes soft-deleted ones, tenant filter
        // scopes the tenant) must never point at a missing subject.
        var inUse = await courseRepository.Any(c => c.SubjectId == request.SubjectId, cancellationToken);
        if (inUse)
            return Error.Conflict("Subject.InUse", "Cannot delete a subject that is still referenced by a course.");

        var result = subject.Delete();
        if (result.IsError) return result.Errors;

        subjectRepository.Update(subject);
        logger.LogInformation("Subject {SubjectId} soft-deleted.", request.SubjectId);

        return Result.Success;
    }
}
