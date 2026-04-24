using AlphaZero.Modules.Assessments.Domain.Enums;
using AlphaZero.Modules.Assessments.Domain.Events;
using AlphaZero.Modules.Assessments.Domain.Models.Content;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments.Servies;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments;

public class Assessment : TenantOwnedAggregate, ISoftDeletable
{
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public AssessmentType Type { get; private set; }
    public decimal PassingScore { get; private set; }
    public AssessmentStatus Status { get; private set; }
    
    // The JSONB content
    public AssessmentContent Content { get; private set; } = new();

    public bool IsDeleted { get; private set; }
    public DateTime? OnDeleted { get; private set; }

    private Assessment() { } // For EF Core

    private Assessment(Guid id, Guid tenantId, string title, string? description, AssessmentType type, decimal passingScore) 
        : base(id, tenantId)
    {
        Title = title;
        Description = description;
        Type = type;
        PassingScore = passingScore;
        Status = AssessmentStatus.Draft;
    }

    public static ErrorOr<Assessment> Create(Guid id, Guid tenantId, string title, string? description, AssessmentType type, decimal passingScore)
    {
        if (string.IsNullOrWhiteSpace(title)) return Error.Validation("Assessment.Title", "Title is required.");
        if (passingScore < 0) return Error.Validation("Assessment.PassingScore", "Passing score cannot be negative.");

        return new Assessment(id, tenantId, title, description, type, passingScore);
    }

    public ErrorOr<Success> UpdateContent(AssessmentContent content, IAssestmentValidator validator)
    {
        if (Status == AssessmentStatus.Archived)
            return Error.Conflict("Assessment.Status", "Cannot update content of an archived assessment.");

        if (validator.AssessmentType != this.Type)
            return Error.Unexpected("Assessment.ValidatorMismatch", "The provided validator does not match the assessment type.");

        var validationResult = validator.Validate(content);
        if (validationResult.IsError) return validationResult.Errors;

        Content = content;
        return Result.Success;
    }

    public ErrorOr<Success> Publish()
    {
        if (Status == AssessmentStatus.Published)
            return Error.Conflict("Assessment.Status", "Assessment is already published.");

        if (Content.Items.Count(i => i.Type == ItemType.Question) == 0)
            return Error.Validation("Assessment.Empty", "Assessment must have at least one question before publishing.");

        Status = AssessmentStatus.Published;
        AddDomainEvent(new AssessmentPublishedDomainEvent(Id));
        return Result.Success;
    }

    public ErrorOr<Success> Archive()
    {
        if (Status == AssessmentStatus.Archived)
            return Error.Conflict("Assessment.Status", "Assessment is already archived.");

        Status = AssessmentStatus.Archived;
        return Result.Success;
    }
}

public enum AssessmentStatus
{
    Draft,
    Published,
    Archived
}
