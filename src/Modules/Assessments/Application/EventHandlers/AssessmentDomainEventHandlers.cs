using AlphaZero.Modules.Assessments.Application.Repositories;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments;
using AlphaZero.Modules.Assessments.Domain.Events;
using AlphaZero.Modules.Assessments.IntegrationEvents;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Assessments.Application.EventHandlers;

public class AssessmentDomainEventHandlers : 
    INotificationHandler<AssessmentCreatedDomainEvent>,
    INotificationHandler<AssessmentMetadataUpdatedDomainEvent>,
    INotificationHandler<AssessmentPublishedDomainEvent>
{
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<AssessmentDomainEventHandlers> _logger;

    public AssessmentDomainEventHandlers(
        IAssessmentRepository assessmentRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<AssessmentDomainEventHandlers> logger)
    {
        _assessmentRepository = assessmentRepository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(AssessmentCreatedDomainEvent notification, CancellationToken ct)
    {
        var integrationEvent = new AssessmentMetadataChangedIntegrationEvent(
            notification.AssessmentId,
            notification.Title,
            notification.Type,
            notification.PassingScore,
            "Draft");

        await _publishEndpoint.Publish(integrationEvent, ct);
        _logger.LogInformation("Published MetadataChanged integration event for new Assessment {Id}", notification.AssessmentId);
    }

    public async Task Handle(AssessmentMetadataUpdatedDomainEvent notification, CancellationToken ct)
    {
        var assessment = await _assessmentRepository.GetByIdAsync(notification.AssessmentId, ct);
        if (assessment == null) return;

        var integrationEvent = new AssessmentMetadataChangedIntegrationEvent(
            notification.AssessmentId,
            notification.Title,
            notification.Type,
            notification.PassingScore,
            assessment.Status.ToString());

        await _publishEndpoint.Publish(integrationEvent, ct);
        _logger.LogInformation("Published MetadataChanged integration event for updated Assessment {Id}", notification.AssessmentId);
    }

    public async Task Handle(AssessmentPublishedDomainEvent notification, CancellationToken ct)
    {
        var assessment = await _assessmentRepository.GetByIdAsync(notification.AssessmentId, ct);
        if (assessment == null) return;

        var integrationEvent = new AssessmentMetadataChangedIntegrationEvent(
            assessment.Id,
            assessment.Title,
            assessment.Type.ToString(),
            assessment.PassingScore,
            "Published");

        await _publishEndpoint.Publish(integrationEvent, ct);
        _logger.LogInformation("Published MetadataChanged integration event for published Assessment {Id}", assessment.Id);
    }
}
