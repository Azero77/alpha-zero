using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class Course : TenantOwnedAggregate
{
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public Guid SubjectId { get; private set; }
    public CourseStatus Status { get; private set; }
    public int NextAvailableBitIndex { get; private set; } 

    public IReadOnlyCollection<CourseSection> Sections => _sections.AsReadOnly();
    private readonly List<CourseSection> _sections = new();

    private Course(Guid id, Guid tenantId, string title, string? description, Guid subjectId) : base(id, tenantId)
    {
        Title = title;
        Description = description;
        SubjectId = subjectId;
        Status = CourseStatus.Draft;
        NextAvailableBitIndex = 0;
    }

    public static ErrorOr<Course> Create(Guid id, Guid tenantId, string title, string? description, Guid subjectId)
    {
        if (string.IsNullOrWhiteSpace(title)) return Error.Validation("Course.Title", "Title is required.");
        return new Course(id, tenantId, title, description, subjectId);
    }

    public void AddSection(string title)
    {
        var section = CourseSection.Create(TenantId, title, _sections.Count);
        _sections.Add(section);
    }

    public ErrorOr<Success> AddLesson(Guid sectionId, string title, Guid videoId)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId);
        if (section == null) return Error.NotFound("Course.Section", "Section not found.");

        var lesson = new CourseSectionLesson(Guid.NewGuid(), TenantId, title, videoId, section.Items.Count, NextAvailableBitIndex++);
        section.AddItem(lesson);
        return Result.Success;
    }

    public ErrorOr<Success> AddQuiz(Guid sectionId, string title, Guid quizId)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId);
        if (section == null) return Error.NotFound("Course.Section", "Section not found.");

        var quiz = new CourseSectionQuiz(Guid.NewGuid(), TenantId, title, quizId, section.Items.Count, NextAvailableBitIndex++);
        section.AddItem(quiz);
        return Result.Success;
    }

    public ErrorOr<Success> ReorderItems(Guid sectionId, List<Guid> itemIds)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId);
        if (section == null) return Error.NotFound("Course.Section", "Section not found.");
        for (int i = 0; i < itemIds.Count; i++)
        {
            var item = section.Items.FirstOrDefault(x => x.Id == itemIds[i]);
            item?.UpdateOrder(i);
        }
        return Result.Success;
    }

    public int TotalTrackedItems => NextAvailableBitIndex;

    public ErrorOr<Success> SubmitForReview()
    {
        if (Status != CourseStatus.Draft) return Error.Conflict("Course.Status", "Only draft courses can be reviewed.");
        if (_sections.Count == 0 || _sections.All(s => s.Items.Count == 0)) 
            return Error.Validation("Course.Empty", "Course must have content before review.");
        Status = CourseStatus.UnderReview;
        return Result.Success;
    }
}


