using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Services;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.State;

// Commands
public record SubmitCourseForReviewCommand(Guid CourseId) : ICommand<Success>;
public record ApproveCourseCommand(Guid CourseId) : ICommand<Success>;
public record RejectCourseCommand(Guid CourseId, string Reason) : ICommand<Success>;
public record PublishCourseCommand(Guid CourseId, bool CanDirectPublish = false) : ICommand<Success>;
public record ArchiveCourseCommand(Guid CourseId) : ICommand<Success>;

// Validators
public class SubmitCourseForReviewCommandValidator : AbstractValidator<SubmitCourseForReviewCommand>
{
    public SubmitCourseForReviewCommandValidator() => RuleFor(x => x.CourseId).NotEmpty();
}

public class ApproveCourseCommandValidator : AbstractValidator<ApproveCourseCommand>
{
    public ApproveCourseCommandValidator() => RuleFor(x => x.CourseId).NotEmpty();
}

public class RejectCourseCommandValidator : AbstractValidator<RejectCourseCommand>
{
    public RejectCourseCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty();
    }
}

public class PublishCourseCommandValidator : AbstractValidator<PublishCourseCommand>
{
    public PublishCourseCommandValidator() => RuleFor(x => x.CourseId).NotEmpty();
}

public class ArchiveCourseCommandValidator : AbstractValidator<ArchiveCourseCommand>
{
    public ArchiveCourseCommandValidator() => RuleFor(x => x.CourseId).NotEmpty();
}

// Handlers
public sealed class CourseStateHandlers : 
    IRequestHandler<SubmitCourseForReviewCommand, ErrorOr<Success>>,
    IRequestHandler<ApproveCourseCommand, ErrorOr<Success>>,
    IRequestHandler<RejectCourseCommand, ErrorOr<Success>>,
    IRequestHandler<PublishCourseCommand, ErrorOr<Success>>,
    IRequestHandler<ArchiveCourseCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ICourseOverviewRepository _courseOverviewRepository;
    private readonly CoursePublishingService _publishingService;
    private readonly ILogger<CourseStateHandlers> _logger;

    public CourseStateHandlers(
        ICourseRepository courseRepository,
        ICourseOverviewRepository courseOverviewRepository,
        CoursePublishingService publishingService,
        ILogger<CourseStateHandlers> logger)
    {
        _courseRepository = courseRepository;
        _courseOverviewRepository = courseOverviewRepository;
        _publishingService = publishingService;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(SubmitCourseForReviewCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.Query(request.CourseId)
            .WithSections()
            .WithItems()
            .ExecuteAsync(cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");
        var result = course.SubmitForReview();
        if (!result.IsError)
        {
             _courseRepository.Update(course);
            _logger.LogInformation("Course {CourseId} submitted for review.", request.CourseId);
        }
        return result;
    }

    public async Task<ErrorOr<Success>> Handle(ApproveCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.Query(request.CourseId)
            .ExecuteAsync(cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");
        var result = course.Approve();
        if (!result.IsError)
        {
            _courseRepository.Update(course);
            _logger.LogInformation("Course {CourseId} approved.", request.CourseId);
        }
        return result;
    }

    public async Task<ErrorOr<Success>> Handle(RejectCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.Query(request.CourseId)
            .ExecuteAsync(cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");
        var result = course.Reject(request.Reason);
        if (!result.IsError)
        {
            _courseRepository.Update(course);
            _logger.LogInformation("Course {CourseId} rejected. Reason: {Reason}", request.CourseId, request.Reason);
        }
        return result;
    }

    public async Task<ErrorOr<Success>> Handle(PublishCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.Query(request.CourseId)
            .WithSections()
            .WithItems()
            .WithResources()
            .WithPlans()
            .ExecuteAsync(cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var overview = await _courseOverviewRepository.GetFirst(o => o.CourseId == request.CourseId, cancellationToken);
        var result = _publishingService.Publish(course, overview, request.CanDirectPublish);
        if (!result.IsError)
        {
            _courseRepository.Update(course);
            _logger.LogInformation("Course {CourseId} published.", request.CourseId);
        }
        return result;
    }

    public async Task<ErrorOr<Success>> Handle(ArchiveCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.Query(request.CourseId)
            .ExecuteAsync(cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");
        var result = course.Archive();
        if (!result.IsError)
        {
            _courseRepository.Update(course);
            _logger.LogInformation("Course {CourseId} archived.", request.CourseId);
        }
        return result;
    }
}
