using AlphaZero.Modules.Assessments.Application.Resolvers;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Submissions;
using AlphaZero.Modules.Assessments.Domain.Models.Submissions;

namespace AlphaZero.Modules.Assessments.Application.Services;

public class AssessmentGradingService
{
    private readonly IEnumerable<IQuestionResolver> _resolvers;

    public AssessmentGradingService(IEnumerable<IQuestionResolver> resolvers)
    {
        _resolvers = resolvers;
    }

    public async Task<decimal> CalculateScoreAsync(Assessment assessment, AssessmentSubmission submission)
    {
        decimal totalScore = 0;

        foreach (var item in assessment.Content.Items.Where(i => i.Type == Domain.Enums.ItemType.Question))
        {
            if (submission.Responses.Answers.TryGetValue(item.Id, out var answer))
            {
                var resolver = _resolvers.FirstOrDefault(r => r.SupportedType == item.QuestionType);
                if (resolver != null)
                {
                    var score = await resolver.ResolveScoreAsync(item, answer);
                    if (score.HasValue)
                    {
                        totalScore += score.Value;
                        // Professional touch: if answer is a complex object, we could enrich it with the score
                    }
                }
            }
        }

        return totalScore;
    }
}
