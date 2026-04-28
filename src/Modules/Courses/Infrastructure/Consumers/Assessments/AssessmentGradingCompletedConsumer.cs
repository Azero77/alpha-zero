using AlphaZero.Modules.Assessments.IntegrationEvents;
using AlphaZero.Modules.Courses.Application.Enrollements.Commands.CompleteItem;
using AlphaZero.Modules.Courses.Application.Repositories;
using Autofac;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers.Assessments;

public class AssessmentGradingCompletedConsumer : IConsumer<AssessmentGradingCompletedIntegrationEvent>
{
    private readonly ICoursesModule _coursesModule;
    private readonly ILogger<AssessmentGradingCompletedConsumer> _logger;

    public AssessmentGradingCompletedConsumer(
        ICoursesModule coursesModule,
        ILogger<AssessmentGradingCompletedConsumer> logger)
    {
        _coursesModule = coursesModule;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AssessmentGradingCompletedIntegrationEvent> context)
    {
        var message = context.Message;

        if (!message.IsSuccess)
        {
            _logger.LogInformation("Assessment {AssessmentId} for student {StudentId} failed. Progress not updated.", message.AssessmentId, message.StudentId);
            return;
        }

        using var scope = _coursesModule.CreateScope();
        var courseRepository = scope.Resolve<ICourseRepository>();
        var enrollementRepository = scope.Resolve<IEnrollementRepository>();

        // 1. Find the Course and BitIndex for this Assessment
        var itemInfo = await courseRepository.GetItemBitIndexByResourceIdAsync(message.AssessmentId);
        if (itemInfo == null)
        {
            _logger.LogWarning("Course item for Assessment {AssessmentId} not found. Cannot update progress.", message.AssessmentId);
            return;
        }

        // 2. Find the Enrollment for this student and course
        var enrollment = await enrollementRepository.GetFirst(
            e => e.StudentId == message.StudentId && e.CourseId == itemInfo.Value.CourseId);

        if (enrollment == null)
        {
            _logger.LogWarning("Enrollment for student {StudentId} in course {CourseId} not found.", message.StudentId, itemInfo.Value.CourseId);
            return;
        }

        // 3. Mark the item as complete
        var command = new CompleteItemCommand(enrollment.Id, itemInfo.Value.BitIndex);
        var result = await _coursesModule.Send(command);

        if (result.IsError)
        {
            _logger.LogError("Failed to update course progress for student {StudentId}. Errors: {Errors}", 
                message.StudentId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        _logger.LogInformation("Course progress updated for Student {StudentId} in Course {CourseId} (BitIndex: {BitIndex}).", 
            message.StudentId, itemInfo.Value.CourseId, itemInfo.Value.BitIndex);
    }
}
