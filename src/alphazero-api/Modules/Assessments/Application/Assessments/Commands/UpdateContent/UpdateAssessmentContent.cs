using AlphaZero.Modules.Assessments.Application.Repositories;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments.Servies;
using AlphaZero.Modules.Assessments.Domain.Models.Content;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Assessments.Application.Assessments.Commands.UpdateContent;

public record UpdateAssessmentContentCommand(
    Guid AssessmentId,
    AssessmentContent Content) : ICommand<Success>;

public class UpdateAssessmentContentCommandValidator : AbstractValidator<UpdateAssessmentContentCommand>
{
    public UpdateAssessmentContentCommandValidator()
    {
        RuleFor(x => x.AssessmentId).NotEmpty();
        RuleFor(x => x.Content).NotNull();
    }
}

public sealed class UpdateAssessmentContentCommandHandler : IRequestHandler<UpdateAssessmentContentCommand, ErrorOr<Success>>
{
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly IAssestmentValidtorFactory _validatorFactory;
    private readonly ILogger<UpdateAssessmentContentCommandHandler> _logger;

    public UpdateAssessmentContentCommandHandler(
        IAssessmentRepository assessmentRepository,
        IAssestmentValidtorFactory validatorFactory,
        ILogger<UpdateAssessmentContentCommandHandler> logger)
    {
        _assessmentRepository = assessmentRepository;
        _validatorFactory = validatorFactory;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateAssessmentContentCommand request, CancellationToken cancellationToken)
    {
        var assessment = await _assessmentRepository.GetByIdAsync(request.AssessmentId, cancellationToken);
        if (assessment is null) return Error.NotFound("Assessment.NotFound", "Assessment not found.");

        var validator = _validatorFactory.CreateValidator(assessment.Type);
        
        var result = assessment.UpdateContent(request.Content, validator);
        if (result.IsError) return result.Errors;

        _assessmentRepository.Update(assessment);
        _logger.LogInformation("Content updated for Assessment {AssessmentId}.", request.AssessmentId);

        return Result.Success;
    }
}
