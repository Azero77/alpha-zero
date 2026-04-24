using AlphaZero.Modules.Assessments.Application.Repositories;
using AlphaZero.Modules.Assessments.Application.Services;
using AlphaZero.Modules.Assessments.Domain.Models.Submissions;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Assessments.Application.Submissions.Commands.Submit;

public record SubmitAssessmentCommand(Guid SubmissionId, AssessmentSubmissionResponses Responses) : ICommand<decimal?>;

public sealed class SubmitAssessmentCommandHandler : IRequestHandler<SubmitAssessmentCommand, ErrorOr<decimal?>>
{
    private readonly ISubmissionRepository _submissionRepository;
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly AssessmentGradingService _gradingService;
    private readonly ILogger<SubmitAssessmentCommandHandler> _logger;

    public SubmitAssessmentCommandHandler(
        ISubmissionRepository submissionRepository,
        IAssessmentRepository assessmentRepository,
        AssessmentGradingService gradingService,
        ILogger<SubmitAssessmentCommandHandler> logger)
    {
        _submissionRepository = submissionRepository;
        _assessmentRepository = assessmentRepository;
        _gradingService = gradingService;
        _logger = logger;
    }

    public async Task<ErrorOr<decimal?>> Handle(SubmitAssessmentCommand request, CancellationToken ct)
    {
        var submission = await _submissionRepository.GetByIdAsync(request.SubmissionId, ct);
        if (submission is null) return Error.NotFound("Submission.NotFound", "Submission not found.");

        // Optimized load: only fetch the version student used
        var assessment = await _assessmentRepository.GetByIdWithVersionAsync(submission.AssessmentId, submission.AssessmentVersionId, ct);
        if (assessment is null) return Error.NotFound("Assessment.NotFound", "Assessment not found.");

        // 1. Update submission state to Submitted
        var submitResult = submission.Submit(request.Responses);
        if (submitResult.IsError) return submitResult.Errors;

        // 2. Perform Grading
        decimal totalScore = await _gradingService.CalculateScoreAsync(assessment, submission);

        // 3. Determine if manual review is needed using the loaded version
        var version = assessment.Versions.First();
        bool needsManualReview = version.Content.Items
            .Any(i => i.Type == Domain.Enums.ItemType.Question && 
                      (i.QuestionType == Domain.Enums.QuestionType.Handwritten || 
                       i.QuestionType == Domain.Enums.QuestionType.Voice ||
                       i.QuestionType == Domain.Enums.QuestionType.Video));

        if (needsManualReview)
        {
            submission.MarkAsUnderReview();
            _logger.LogInformation("Submission {SubmissionId} marked for manual review.", submission.Id);
        }
        else
        {
            submission.FinalizeGrading(totalScore);
            _logger.LogInformation("Submission {SubmissionId} graded automatically. Score: {Score}", submission.Id, totalScore);
        }

        _submissionRepository.Update(submission);
        return submission.TotalScore;
    }
}
