using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.State;

// Commands
public record SubmitCourseForReviewCommand(Guid CourseId) : ICommand<Success>;
public record ApproveCourseCommand(Guid CourseId) : ICommand<Success>;
public record RejectCourseCommand(Guid CourseId, string Reason) : ICommand<Success>;
public record PublishCourseCommand(Guid CourseId) : ICommand<Success>;
public record ArchiveCourseCommand(Guid CourseId) : ICommand<Success>;

// Handlers
public sealed class CourseStateHandlers : 
    IRequestHandler<SubmitCourseForReviewCommand, ErrorOr<Success>>,
    IRequestHandler<ApproveCourseCommand, ErrorOr<Success>>,
    IRequestHandler<RejectCourseCommand, ErrorOr<Success>>,
    IRequestHandler<PublishCourseCommand, ErrorOr<Success>>,
    IRequestHandler<ArchiveCourseCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<CourseStateHandlers> _logger;

    public CourseStateHandlers(ICourseRepository courseRepository, ILogger<CourseStateHandlers> logger)
    {
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(SubmitCourseForReviewCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdWithSectionsAsync(request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");
        var result = course.SubmitForReview();
        if (!result.IsError) _logger.LogInformation("Course {CourseId} submitted for review.", request.CourseId);
        return result;
    }

    public async Task<ErrorOr<Success>> Handle(ApproveCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetFirst(c => c.Id == request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");
        var result = course.Approve();
        if (!result.IsError) _logger.LogInformation("Course {CourseId} approved.", request.CourseId);
        return result;
    }

    public async Task<ErrorOr<Success>> Handle(RejectCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetFirst(c => c.Id == request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");
        var result = course.Reject(request.Reason);
        if (!result.IsError) _logger.LogInformation("Course {CourseId} rejected. Reason: {Reason}", request.CourseId, request.Reason);
        return result;
    }

    public async Task<ErrorOr<Success>> Handle(PublishCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetFirst(c => c.Id == request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");
        var result = course.Publish();
        if (!result.IsError) _logger.LogInformation("Course {CourseId} published.", request.CourseId);
        return result;
    }

    public async Task<ErrorOr<Success>> Handle(ArchiveCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetFirst(c => c.Id == request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");
        var result = course.Archive();
        if (!result.IsError) _logger.LogInformation("Course {CourseId} archived.", request.CourseId);
        return result;
    }
}
