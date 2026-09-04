using AlphaZero.Modules.Assessments.Application.Repositories;
using AlphaZero.Modules.Assessments.Domain.Events;
using AlphaZero.Modules.Assessments.IntegrationEvents;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Assessments.Application.EventHandlers;

public class AssessmentGradingCompletedDomainEventHandler : INotificationHandler<AssessmentGradingCompletedDomainEvent>
{
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<AssessmentGradingCompletedDomainEventHandler> _logger;

    public AssessmentGradingCompletedDomainEventHandler(
        IAssessmentRepository assessmentRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<AssessmentGradingCompletedDomainEventHandler> logger)
    {
        _assessmentRepository = assessmentRepository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(AssessmentGradingCompletedDomainEvent notification, CancellationToken ct)
    {
        var assessment = await _assessmentRepository.GetByIdAsync(notification.AssessmentId, ct);
        if (assessment is null)
        {
            _logger.LogError("Assessment {AssessmentId} not found while handling grading completed event.", notification.AssessmentId);
            return;
        }

        bool isSuccess = notification.Score >= assessment.PassingScore;

        var integrationEvent = new AssessmentGradingCompletedIntegrationEvent(
            notification.SubmissionId,
            notification.AssessmentId,
            notification.StudentId,
            notification.Score,
            isSuccess);

        await _publishEndpoint.Publish(integrationEvent, ct);

        _logger.LogInformation("Integration Event Published: AssessmentGradingCompleted for Student {StudentId}, Success: {IsSuccess}", 
            notification.StudentId, isSuccess);
    }
}
