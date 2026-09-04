namespace AlphaZero.Modules.Assessments.Domain.Models.Submissions;

public class AssessmentSubmissionResponses
{
    // For MCQ: Dictionary<QuestionId, ChoiceId>
    // For Hybrid: Dictionary<QuestionId, SubmissionItem>
    public Dictionary<string, object> Answers { get; set; } = new();
}

public class SubmissionItem
{
    public object? Value { get; set; } // ChoiceId, Text, etc.
    public string? MediaUrl { get; set; } // For handwritten/voice/video
    public decimal? Score { get; set; }
    public string? TeacherFeedback { get; set; }
    public AiGradingInfo? AiGrading { get; set; }
}

public class AiGradingInfo
{
    public string? TranscribedText { get; set; }
    public decimal? SuggestedScore { get; set; }
    public string? AiFeedback { get; set; }
    public decimal Confidence { get; set; }
}
