using AlphaZero.Modules.Assessments.Domain.Enums;
using AlphaZero.Modules.Assessments.Domain.Models.Content;

namespace AlphaZero.Modules.Assessments.Application.Resolvers;

public class McqQuestionResolver : IQuestionResolver
{
    public QuestionType SupportedType => QuestionType.MCQ;

    public Task<decimal?> ResolveScoreAsync(AssessmentItem question, object responseValue)
    {
        if (question.GradingData == null || string.IsNullOrEmpty(question.GradingData.CorrectChoiceId))
            return Task.FromResult<decimal?>(0);

        // In MCQ, the responseValue is expected to be the ChoiceId string
        var submittedChoiceId = responseValue?.ToString();

        if (submittedChoiceId == question.GradingData.CorrectChoiceId)
        {
            return Task.FromResult<decimal?>(question.Points ?? 0);
        }

        return Task.FromResult<decimal?>(0);
    }
}
