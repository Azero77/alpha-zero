using AlphaZero.Modules.Assessments.Domain.Enums;
using System.Text.Json.Serialization;

namespace AlphaZero.Modules.Assessments.Domain.Models.Content;

public class AssessmentContent
{
    public string Version { get; set; } = "1.0";
    public List<AssessmentItem> Items { get; set; } = new();
}

public class AssessmentItem
{
    public string Id { get; set; } = default!;
    public ItemType Type { get; set; }
    
    // Editor concern - backend ignores internal structure
    public object RenderData { get; set; } = default!;

    // Only present if Type == Question
    public QuestionType? QuestionType { get; set; }
    public decimal? Points { get; set; }
    public GradingData? GradingData { get; set; }
}

public class GradingData
{
    // For MCQ
    public List<Choice>? Choices { get; set; }
    public string? CorrectChoiceId { get; set; }
    public bool ShuffleOptions { get; set; }

    // For Handwritten/Media
    public string? Rubric { get; set; }
    public string? AiHint { get; set; }
}

public class Choice
{
    public string Id { get; set; } = default!;
    public object RenderData { get; set; } = default!;
}
