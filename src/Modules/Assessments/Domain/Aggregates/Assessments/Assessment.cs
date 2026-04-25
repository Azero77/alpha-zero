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
    
    public Guid? CurrentVersionId { get; private set; }
    
    private readonly List<AssessmentVersion> _versions = new();
    public IReadOnlyCollection<AssessmentVersion> Versions => _versions.AsReadOnly();

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

        var assessment = new Assessment(id, tenantId, title, description, type, passingScore);
        assessment.AddDomainEvent(new AssessmentCreatedDomainEvent(id, title, type.ToString(), passingScore));
        return assessment;
    }

    public ErrorOr<Success> UpdateInformation(string title, string? description, decimal passingScore)
    {
        if (Status == AssessmentStatus.Archived)
            return Error.Conflict("Assessment.Status", "Cannot update archived assessment.");

        Title = title;
        Description = description;
        PassingScore = passingScore;

        AddDomainEvent(new AssessmentCreatedDomainEvent(Id, Title, Type.ToString(), PassingScore)); // Reuse event or create MetadataChanged
        return Result.Success;
    }

    public ErrorOr<Success> UpdateContent(AssessmentContent content, IAssestmentValidator validator)
    {
        if (Status == AssessmentStatus.Archived)
            return Error.Conflict("Assessment.Status", "Cannot update content of an archived assessment.");

        if (validator.AssessmentType != this.Type)
            return Error.Unexpected("Assessment.ValidatorMismatch", "The provided validator does not match the assessment type.");

        var validationResult = validator.Validate(content);
        if (validationResult.IsError) return validationResult.Errors;

        var nextVersionNumber = _versions.Count + 1;
        var newVersion = new AssessmentVersion(Guid.NewGuid(), TenantId, Id, nextVersionNumber, content);
        
        _versions.Add(newVersion);
        CurrentVersionId = newVersion.Id;

        return Result.Success;
    }

    public ErrorOr<Success> Publish()
    {
        if (Status == AssessmentStatus.Published)
            return Error.Conflict("Assessment.Status", "Assessment is already published.");

        if (CurrentVersionId == null)
            return Error.Validation("Assessment.Empty", "Assessment must have content before publishing.");

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

    public bool NeedsManualReview(Guid version)
    {
        AssessmentVersion? assessmentVersion = _versions.FirstOrDefault(v => version == v.Id);

        if(assessmentVersion == null) return false;
        return assessmentVersion.Content.Items
           .Any(i => i.Type == Domain.Enums.ItemType.Question &&
                     (i.QuestionType == Domain.Enums.QuestionType.Handwritten ||
                      i.QuestionType == Domain.Enums.QuestionType.Voice ||
                      i.QuestionType == Domain.Enums.QuestionType.Video));
    }
}

public enum AssessmentStatus
{
    Draft,
    Published,
    Archived
}
