using AlphaZero.Modules.Assessments.Domain.Enums;
using AlphaZero.Modules.Assessments.Domain.Models.Content;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments.Servies;

public interface IAssestmentValidator
{
    //Validation for the content to match the type for the assestment
    AssessmentType AssessmentType { get; }
    ErrorOr<Success> Validate(AssessmentContent assessmentContent);
}


public interface IAssestmentValidtorFactory
{
        IAssestmentValidator CreateValidator(AssessmentType assessmentType);
}

public class AssestmentValidtorFactory : IAssestmentValidtorFactory
{
    private readonly IServiceProvider _serviceProvider;
    public AssestmentValidtorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    public IAssestmentValidator CreateValidator(AssessmentType assessmentType)
    {
        return assessmentType switch
        {
            AssessmentType.MCQ => _serviceProvider.GetRequiredService<McqAssessmentValidator>(),
            AssessmentType.Handwritten => _serviceProvider.GetRequiredService<HandwrittenAssessmentValidator>(),
            AssessmentType.Hybrid => _serviceProvider.GetRequiredService<HybridAssessmentValidator>(),
            _ => throw new NotSupportedException($"Unsupported assessment type: {assessmentType}")
        };
    }
}

public class McqAssessmentValidator : IAssestmentValidator
{
    public AssessmentType AssessmentType => AssessmentType.MCQ;
    public ErrorOr<Success> Validate(AssessmentContent assessmentContent)
    {
        if (assessmentContent.Items.Any(i => i.Type == ItemType.Question && i.QuestionType != QuestionType.MCQ))
            return Error.Validation("Assessment.Content", "MCQ Assessments can only contain MCQ questions.");
        return Result.Success;
    }
}

public class HandwrittenAssessmentValidator : IAssestmentValidator
{
    public AssessmentType AssessmentType => AssessmentType.Handwritten;
    public ErrorOr<Success> Validate(AssessmentContent assessmentContent)
    {
        if (assessmentContent.Items.Any(i => i.Type == ItemType.Question && i.QuestionType != QuestionType.Handwritten))
            return Error.Validation("Assessment.Content", "Handwritten Assessments can only contain Handwritten questions.");
        return Result.Success;
    }
}

public class HybridAssessmentValidator : IAssestmentValidator
{
    public AssessmentType AssessmentType => throw new NotImplementedException();

    public ErrorOr<Success> Validate(AssessmentContent assessmentContent)
    {
        return Result.Success;
    }
}