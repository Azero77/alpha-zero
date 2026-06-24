using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.IntegrationEvents;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers.Analytics;

public class CourseAnalyticsUpdaterConsumer :
    IConsumer<EnrollementCreatedIntegrationEvent>,
    IConsumer<ItemCompletedIntegrationEvent>
{
    private readonly ICourseAnalyticsRepository _repository;
    private readonly ILogger<CourseAnalyticsUpdaterConsumer> _logger;

    public CourseAnalyticsUpdaterConsumer(
        ICourseAnalyticsRepository repository,
        ILogger<CourseAnalyticsUpdaterConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EnrollementCreatedIntegrationEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Updating analytics for new enrollment in Course {CourseId}", msg.CourseId);
        await _repository.IncrementEnrollmentCountAsync(msg.CourseId, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<ItemCompletedIntegrationEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Updating analytics for item completion {BitIndex} in Course {CourseId}", msg.BitIndex, msg.CourseId);
        
        double diff = msg.NewCompletionPercentage - msg.OldCompletionPercentage;
        await _repository.IncrementItemCompletionAsync(msg.CourseId, msg.BitIndex, diff, context.CancellationToken);
    }
}
