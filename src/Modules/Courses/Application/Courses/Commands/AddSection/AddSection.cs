using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.AddSection;

public record AddSectionCommand(Guid CourseId, string Title) : ICommand<Success>;

public class AddSectionCommandValidator : AbstractValidator<AddSectionCommand>
{
    public AddSectionCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}

public sealed class AddSectionCommandHandler : IRequestHandler<AddSectionCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<AddSectionCommandHandler> _logger;

    public AddSectionCommandHandler(ICourseRepository courseRepository, ILogger<AddSectionCommandHandler> logger)
    {
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(AddSectionCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdWithSectionsAsync(request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        course.AddSection(request.Title);
        _logger.LogInformation("Section '{Title}' added to Course {CourseId}.", request.Title, request.CourseId);

        return Result.Success;
    }
}
