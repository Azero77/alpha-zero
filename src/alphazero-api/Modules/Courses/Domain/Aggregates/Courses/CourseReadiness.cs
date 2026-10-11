namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public record CourseReadiness(
    bool CanPublish,
    bool CanSubmitForReview,
    CourseReadinessChecks Checks,
    IReadOnlyList<string> BlockingReasons);

public record CourseReadinessChecks(
    OverviewReadinessChecks Overview,
    CurriculumReadinessChecks Curriculum,
    CommercialReadinessChecks Commercial);

public record OverviewReadinessChecks(
    bool HasTitle,
    bool HasSubject,
    bool HasDescription,
    bool HasCoverImage);

public record CurriculumReadinessChecks(
    bool HasSections,
    bool HasItems,
    bool AllAttachedAssetsReady);

public record CommercialReadinessChecks(
    bool HasActivePricingPlan);
