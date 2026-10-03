using System;
using System.Text.Json;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class CourseOverview : TenantOwnedEntity
{
    public Guid CourseId { get; private set; }
    
    public JsonDocument DescriptionContent { get; private set; }
    public JsonDocument? TargetAudienceContent { get; private set; }
    public JsonDocument? LearningObjectivesContent { get; private set; }
    
    public Guid? CoverImageDocumentId { get; private set; }
    
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    
    public string ETag { get; private set; }

    // Required by EF Core
    private CourseOverview() : base(default, default) 
    {
        DescriptionContent = JsonDocument.Parse("{}");
        ETag = "";
    }

    private CourseOverview(
        Guid id,
        Guid tenantId,
        Guid courseId,
        JsonDocument descriptionContent,
        JsonDocument? targetAudienceContent,
        JsonDocument? learningObjectivesContent,
        Guid? coverImageDocumentId) : base(id, tenantId)
    {
        CourseId = courseId;
        DescriptionContent = descriptionContent;
        TargetAudienceContent = targetAudienceContent;
        LearningObjectivesContent = learningObjectivesContent;
        CoverImageDocumentId = coverImageDocumentId;
        
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
        UpdateETag();
    }

    public static CourseOverview Create(
        Guid id,
        Guid tenantId,
        Guid courseId,
        JsonDocument descriptionContent,
        JsonDocument? targetAudienceContent,
        JsonDocument? learningObjectivesContent,
        Guid? coverImageDocumentId)
    {
        return new CourseOverview(
            id,
            tenantId,
            courseId,
            descriptionContent,
            targetAudienceContent,
            learningObjectivesContent,
            coverImageDocumentId);
    }

    public void Update(
        JsonDocument descriptionContent,
        JsonDocument? targetAudienceContent,
        JsonDocument? learningObjectivesContent,
        Guid? coverImageDocumentId)
    {
        DescriptionContent = descriptionContent;
        TargetAudienceContent = targetAudienceContent;
        LearningObjectivesContent = learningObjectivesContent;
        CoverImageDocumentId = coverImageDocumentId;
        
        UpdatedAtUtc = DateTime.UtcNow;
        UpdateETag();
    }

    private void UpdateETag()
    {
        // Simple ETag generation based on updated time
        ETag = $"\"{Guid.NewGuid():N}\"";
    }
}
