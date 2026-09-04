using AlphaZero.Modules.Assessments.Domain.Enums;
using AlphaZero.Modules.Assessments.Domain.Models.Content;
using AlphaZero.Modules.Assessments.Domain.Models.Submissions;

namespace AlphaZero.Modules.Assessments.Application.Resolvers;

public interface IQuestionResolver
{
    QuestionType SupportedType { get; }
    
    /// <summary>
    /// Resolves the score for a specific question.
    /// If the result is null, it means manual review is needed.
    /// </summary>
    Task<decimal?> ResolveScoreAsync(AssessmentItem question, object responseValue);
}
