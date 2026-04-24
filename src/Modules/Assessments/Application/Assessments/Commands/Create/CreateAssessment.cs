using AlphaZero.Modules.Assessments.Application.Repositories;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments.Servies;
using AlphaZero.Modules.Assessments.Domain.Enums;
using AlphaZero.Modules.Assessments.Domain.Models.Content;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Assessments.Application.Assessments.Commands.Create;

public record CreateAssessmentCommand(
    string Title, 
    string? Description, 
    AssessmentType Type, 
    decimal PassingScore,
    AssessmentContent? InitialContent = null) : ICommand<Guid>;

public class CreateAssessmentCommandValidator : AbstractValidator<CreateAssessmentCommand>
{
    public CreateAssessmentCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.PassingScore).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateAssessmentCommandHandler : IRequestHandler<CreateAssessmentCommand, ErrorOr<Guid>>
{
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<CreateAssessmentCommandHandler> _logger;

    public CreateAssessmentCommandHandler(
        IAssessmentRepository assessmentRepository,
        ITenantProvider tenantProvider,
        ILogger<CreateAssessmentCommandHandler> logger,
        IAssestmentValidtorFactory assestmentValidtorFactory)
    {
        _assessmentRepository = assessmentRepository;
        _tenantProvider = tenantProvider;
        _logger = logger;
        _assestmentValidtorFactory = assestmentValidtorFactory;
    }

    private readonly IAssestmentValidtorFactory _assestmentValidtorFactory;

    public async Task<ErrorOr<Guid>> Handle(CreateAssessmentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        var assessmentId = Guid.NewGuid();
        var assessmentResult = Assessment.Create(
            assessmentId, 
            tenantId.Value, 
            request.Title, 
            request.Description, 
            request.Type, 
            request.PassingScore);

        if (assessmentResult.IsError) return assessmentResult.Errors;

        var assessment = assessmentResult.Value;
        
        if (request.InitialContent != null)
        {
            assessment.UpdateContent(request.InitialContent,_assestmentValidtorFactory.CreateValidator(request.Type));
        }

        _assessmentRepository.Add(assessment);
        _logger.LogInformation("Assessment {AssessmentId} created of type {Type}.", assessmentId, request.Type);

        return assessmentId;
    }
}
