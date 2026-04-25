using AlphaZero.Modules.Assessments.IntegrationEvents;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.AddAssessment;

public record AddAssessmentCommand(Guid CourseId, Guid SectionId, string Title,CreateAssessmentRequest AssessmentRequest) : ICommand<Success>;

public class AddAssessmentCommandValidator : AbstractValidator<AddAssessmentCommand>
{
    public AddAssessmentCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}

public sealed class AddAssessmentCommandHandler : IRequestHandler<AddAssessmentCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<AddAssessmentCommandHandler> _logger;
    IAssessmentService _assessmentService;

    public AddAssessmentCommandHandler(ICourseRepository courseRepository, ILogger<AddAssessmentCommandHandler> logger, IAssessmentService assessmentService)
    {
        _courseRepository = courseRepository;
        _logger = logger;
        _assessmentService = assessmentService;
    }

    public async Task<ErrorOr<Success>> Handle(AddAssessmentCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdWithSectionsAsync(request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var assementCreateReponse = await _assessmentService.AddAssessment(request.AssessmentRequest);
        if (assementCreateReponse.IsError) return assementCreateReponse.Errors;

        var metadata = JsonSerializer.SerializeToElement(request.AssessmentRequest);
        var result = course.AddAssessment(request.SectionId, request.Title, assementCreateReponse.Value.AssessmentId, metadata);
        if (result.IsError) return result.Errors;

        _logger.LogInformation("Assessment '{Title}' added to Section {SectionId} in Course {CourseId}.", request.Title, request.SectionId, request.CourseId);

        return Result.Success;
    }
}


public interface IAssessmentService
{
    Task<ErrorOr<AssessmentCreatedResponse>> AddAssessment(CreateAssessmentRequest request);
}