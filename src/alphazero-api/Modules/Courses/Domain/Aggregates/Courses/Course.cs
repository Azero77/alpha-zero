using System.Collections;
using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class Course : TenantOwnedAggregate, ISoftDeletable
{
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid SubjectId { get; private set; }
    public CourseStatus Status { get; private set; }
    public int NextAvailableBitIndex { get; private set; }

    /// <summary>
    /// One bit per allocated curriculum position: set = the item at that BitIndex is currently
    /// active (not deleted/contained in a deleted section). Cleared positions are deleted items
    /// or never-allocated slots; the Course owns this state, enrollments never replicate it
    /// (ProgressCalculation derives counts from this mask + the student mask on calculation).
    /// Mutations flow exclusively through domain events + CourseActiveItemsDomainEventHandler
    /// (ActivateCurriculumBit / DeactivateCurriculumBits) so curriculum edits and mask updates
    /// are committed in the same SaveChanges transaction.
    /// </summary>
    public BitArray ActiveItemsBitmask { get; private set; } = new BitArray(0, false);

    public IReadOnlyCollection<CourseSection> Sections => _sections.AsReadOnly();
    private readonly List<CourseSection> _sections = new();

    public IReadOnlyCollection<CoursePlan> Plans => _plans.AsReadOnly();
    private readonly List<CoursePlan> _plans = new();

    public bool IsDeleted { get; private set; }
    public DateTime? OnDeleted { get; private set; }

    private Course() : base(default, default) { } // EF Core

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
        var course = new Course(id, tenantId, title, description, subjectId);
        course.AddDomainEvent(new CourseCreatedDomainEvent(id, tenantId));
        return course;
    }

    public ErrorOr<Success> AddSection(string title) => AddSection(Guid.NewGuid(), title);

    public ErrorOr<Success> AddSection(Guid id,string title)
    {
        var section = CourseSection.Create(id,title, _sections.Count, this.Id, null);
        if (section.IsError)
            return section.Errors;
        _sections.Add(section.Value);
        // Sections are containers, not progress-tracked positions: no mask bits are touched here.
        AddDomainEvent(new CourseSectionAddedDomainEvent(Id, section.Value.Id));
        return Result.Success;
    }

    public int AllocateNextBitIndex() => NextAvailableBitIndex++;

    /// <summary>
    /// Single source of truth for curriculum item creation: allocates the stable BitIndex here
    /// (monotonic counter — deleted positions are never reused), delegates to the section and
    /// raises CurriculumItemAddedDomainEvent so the active-items handler activates the new
    /// position atomically with this modification.
    /// </summary>
    public ErrorOr<CurriculumItem> AddItem(
        Guid sectionId,
        string title,
        ResourceType mainType,
        IEnumerable<CurriculumResource>? resources = null)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId && !s.IsDeleted);
        if (section is null)
            return Error.NotFound("Course.Section", "Section not found.");

        var bitIndex = AllocateNextBitIndex();
        var itemResult = section.AddItem(Guid.NewGuid(), title, mainType, bitIndex, resources);
        if (itemResult.IsError)
        {
            // Do not burn an allocation slot for a failed add.
            NextAvailableBitIndex--;
            return itemResult.Errors;
        }

        AddDomainEvent(new CurriculumItemAddedDomainEvent(Id, section.Id, itemResult.Value.Id, bitIndex));
        return itemResult.Value;
    }

    /// <summary>
    /// Soft-deletes a curriculum item. The BitIndex is captured BEFORE removal so the event payload
    /// is deterministic; other indices are never shifted and the position is never reused.
    /// </summary>
    public ErrorOr<Success> RemoveItem(Guid sectionId, Guid itemId)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId && !s.IsDeleted);
        if (section is null)
            return Error.NotFound("Course.Section", "Section not found.");

        var item = section.GetItem(itemId);
        if (item is null)
            return Error.NotFound("CurriculumItem.NotFound", "Item not found in section.");

        var bitIndex = item.BitIndex;
        var removeResult = section.RemoveItem(itemId);
        if (removeResult.IsError)
            return removeResult.Errors;

        AddDomainEvent(new CurriculumItemDeletedDomainEvent(Id, section.Id, itemId, bitIndex));
        return Result.Success;
    }

    public ErrorOr<CoursePlan> AddPlan(string name, Guid principalId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation("Course.PlanName", "Plan name is required.");
        
        if (_plans.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            return Error.Conflict("Course.PlanExists", $"A plan with name '{name}' already exists.");

        var plan = CoursePlan.Create(Id, name, principalId);
        _plans.Add(plan);
        return plan;
    }

    public ErrorOr<Success> UpdatePlan(Guid planId, string name, Guid principalId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation("Course.PlanName", "Plan name is required.");

        var plan = _plans.FirstOrDefault(p => p.Id == planId);
        if (plan == null) return Error.NotFound("Course.Plan", "Plan not found.");

        if (_plans.Any(p => p.Id != planId && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            return Error.Conflict("Course.PlanExists", $"A plan with name '{name}' already exists.");

        plan.Update(name, principalId);
        return Result.Success;
    }

    public ErrorOr<Success> RemovePlan(Guid planId)
    {
        var plan = _plans.FirstOrDefault(p => p.Id == planId);
        if (plan == null) return Error.NotFound("Course.Plan", "Plan not found.");

        _plans.Remove(plan);
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

    public void UpdateInformation(string title, string? description, Guid subjectId)
    {
        Title = title;
        Description = description;
        SubjectId = subjectId;
    }

    public ErrorOr<Success> SubmitForReview()
    {
        if (Status != CourseStatus.Draft) return Error.Conflict("Course.Status", "Only draft courses can be reviewed.");
        if (_sections.Count == 0 || _sections.All(s => s.Items.Count == 0)) 
            return Error.Validation("Course.Empty", "Course must have content before review.");
        Status = CourseStatus.UnderReview;
        return Result.Success;
    }

    public ErrorOr<Success> Approve()
    {
        if (Status != CourseStatus.UnderReview) 
            return Error.Conflict("Course.Status", "Only courses under review can be approved.");
        
        Status = CourseStatus.Approved;
        return Result.Success;
    }

    public ErrorOr<Success> Reject(string reason)
    {
        if (Status != CourseStatus.UnderReview) 
            return Error.Conflict("Course.Status", "Only courses under review can be rejected.");
        if(string.IsNullOrEmpty(reason))
            return Error.Validation("Course.RejectionReason", "Rejection reason is required.");
        Status = CourseStatus.Draft;
        return Result.Success;
    }

    internal ErrorOr<Success> Publish()
    {
        Status = CourseStatus.Published;
        AddDomainEvent(new CoursePublishedDomainEvent(Id));
        return Result.Success;
    }

    public ErrorOr<Success> Archive()
    {
        if (Status == CourseStatus.Archived) 
            return Error.Conflict("Course.Status", "Course is already archived.");
        
        Status = CourseStatus.Archived;
        return Result.Success;
    }

    public ErrorOr<Success> ReorderSections(List<Guid> sectionIds)
    {
        if (Status == CourseStatus.Published)
            return Error.Conflict("Course.Status", "Cannot reorder sections once published as it may confuse existing students.");

        for (int i = 0; i < sectionIds.Count; i++)
        {
            var section = _sections.FirstOrDefault(s => s.Id == sectionIds[i]);
            if (section != null)
            {
                section.Update(section.Title, i);
            }
        }
        return Result.Success;
    }

    public ErrorOr<Success> UpdateSectionTitle(Guid sectionId, string title)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId && !s.IsDeleted);
        if (section == null) return Error.NotFound("Course.Section", "Section not found.");
        section.Update(title, section.Order);
        return Result.Success;
    }

    public ErrorOr<Success> DeleteSection(Guid sectionId)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId && !s.IsDeleted);
        if (section == null) return Error.NotFound("Course.Section", "Section not found.");

        // Capture affected positions BEFORE anything is removed: the handler must be able to
        // deactivate exactly these bits with data it can trust, not reconstructed state.
        var activeBitIndexes = section.Items
            .Where(i => !i.IsDeleted)
            .Select(i => i.BitIndex)
            .ToList();

        var result = section.Delete();
        if (result.IsError) return result.Errors;

        // Soft-delete the contained items too so the aggregate remains self-describing
        // (a Handle restore would preserve the deleted item state of this section).
        foreach (var item in section.Items.Where(i => !i.IsDeleted))
        {
            item.Delete();
        }

        if (activeBitIndexes.Count > 0)
        {
            AddDomainEvent(new CourseSectionDeletedDomainEvent(Id, section.Id, activeBitIndexes));
        }

        return Result.Success;
    }

    public ErrorOr<Success> Delete()
    {
        if (IsDeleted)
            return Error.Conflict("Course.IsDeleted", "Course is already deleted.");

        IsDeleted = true;
        OnDeleted = DateTime.UtcNow;
        return Result.Success;
    }

    public ErrorOr<Success> Restore()
    {
        if (!IsDeleted)
            return Error.Failure("Course.IsDeleted", "Course is not deleted.");

        IsDeleted = false;
        OnDeleted = null;
        return Result.Success;
    }

    // ===================== Active-items mask (event-handler driven) =====================
    // These members are the ONLY mutation path for ActiveItemsBitmask, other than allocation
    // on AddItem. They are invoked exclusively by CourseActiveItemsDomainEventHandler as the
    // in-process translation of the matching domain events, so the curriculum change and the
    // mask update always commit in the same transaction. They are public because the handler
    // lives in the Application layer, but every write still passes through the Course model.

    /// <returns>A defensive copy — callers can never mutate the aggregate's mask directly.</returns>
    public BitArray SnapshotActiveItemsBitmask() => new BitArray(ActiveItemsBitmask);

    public void ActivateCurriculumBit(int bitIndex)
    {
        EnsureBitmaskCapacity(bitIndex + 1);
        ActiveItemsBitmask.Set(bitIndex, true);
    }

    public void DeactivateCurriculumBits(IReadOnlyList<int> bitIndexes)
    {
        foreach (var bitIndex in bitIndexes)
        {
            if (bitIndex < 0 || bitIndex >= ActiveItemsBitmask.Length)
            {
                // Invariant violation, not data to normalize — failing loudly rolls back the
                // transaction instead of committing a mask that disagrees with the curriculum.
                throw new InvalidOperationException($"Cannot deactivate bit index {bitIndex}: outside the allocated range (0-{ActiveItemsBitmask.Length - 1}).");
            }
        }

        foreach (var bitIndex in bitIndexes)
        {
            ActiveItemsBitmask.Set(bitIndex, false);
        }
    }

    /// <summary>
    /// Grows the mask with inactive (zero) bits up to the needed length (BitArray's Length
    /// setter pads on grow). Historical gaps between active positions are preserved —
    /// the mask is never rebuilt, compacted, or truncated.
    /// </summary>
    private void EnsureBitmaskCapacity(int length)
    {
        if (ActiveItemsBitmask.Length < length)
        {
            ActiveItemsBitmask.Length = length;
        }
    }
}
