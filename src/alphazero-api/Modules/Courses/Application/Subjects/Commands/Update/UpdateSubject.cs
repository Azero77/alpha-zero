using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Subject;
using AlphaZero.Shared.Application;
using Microsoft.EntityFrameworkCore;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Subjects.Commands.Update;

public record UpdateSubjectCommand(Guid SubjectId, string Name, string? Description) : ICommand<Success>;

public class UpdateSubjectCommandValidator : AbstractValidator<UpdateSubjectCommand>
{
    public UpdateSubjectCommandValidator()
    {
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
    }
}

public sealed class UpdateSubjectCommandHandler(
    ISubjectRepository subjectRepository,
    ILogger<UpdateSubjectCommandHandler> logger) : IRequestHandler<UpdateSubjectCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(UpdateSubjectCommand request, CancellationToken cancellationToken)
    {
        // Tracked load: this handler mutates the loaded aggregate.
        var subject = await subjectRepository.GetByIdAsync(request.SubjectId, cancellationToken);
        if (subject is null) return Error.NotFound("Subject.NotFound", "Subject not found.");

        var result = subject.Update(request.Name, request.Description);
        if (result.IsError) return result.Errors;

        subjectRepository.Update(subject);
        logger.LogInformation("Subject {SubjectId} updated.", request.SubjectId);

        return Result.Success;
    }
}
