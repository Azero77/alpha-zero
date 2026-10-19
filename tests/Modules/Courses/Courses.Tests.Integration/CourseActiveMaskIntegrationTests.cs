using System.Collections;
using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Courses.Get;
using AlphaZero.Modules.Courses.Presentation.Courses.Items;
using AlphaZero.Modules.Courses.Presentation.Courses.Reorder.Items;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Enroll;
using AlphaZero.Modules.Courses.Presentation.Enrollments.Get;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests.Integration;

/// <summary>
/// Transactional-consistency suite: every curriculum modification (add/delete item, delete
/// section) must arrive on the course's ActiveItemsBitmask in the SAME commit, with enrollment
/// masks untouched and completion percentages derived from the course mask at calculation time.
/// </summary>
public class CourseActiveMaskIntegrationTests : BaseIntegrationTest
{
    public CourseActiveMaskIntegrationTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedSubjectAndCourse(Guid tenantId, string title = "Mask Course")
    {
        SetTenant(tenantId);
        var subResp = await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;
        var courseResp = await Client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = title, SubjectId = subjectId });
        return (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;
    }

    private async Task<Guid> AddSection(Guid courseId, string title = "S")
    {
        var resp = await Client.PostAsJsonAsync($"/courses/{courseId}/sections", new { Title = title });
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var course = await Client.GetFromJsonAsync<CourseResponse>($"/courses/{courseId}");
        return course!.Sections.Last().Id;
    }

    private async Task<ItemResponse> AddItem(Guid courseId, Guid sectionId, string title = "Item")
    {
        var resp = await Client.PostAsJsonAsync(
            $"/courses/{courseId}/sections/{sectionId}/items",
            new CreateCourseSectionItemRequest { CourseId = courseId, SectionId = sectionId, Title = title, MainType = "Video", Content = default });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<ItemResponse>())!;
    }

    private async Task<Guid> Enroll(Guid courseId, Guid studentId)
    {
        var resp = await Client.PostAsJsonAsync("/courses/enroll", new EnrollInCourseRequest { StudentId = studentId, CourseId = courseId });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<EnrollInCourseResponse>())!.EnrollmentId;
    }

    private static void BitsShouldBe(BitArray? mask, params bool[] expected)
    {
        mask.Should().NotBeNull();
        mask!.Length.Should().Be(expected.Length,
            because: "the mask length equals the allocated positions (no compaction, no shifting)");
        for (var i = 0; i < expected.Length; i++)
        {
            mask[i].Should().Be(expected[i], because: $"bit position {i}");
        }
    }

    [Fact]
    public async Task AddSectionThenItems_Should_ActivateExactlyTheAllocatedPositions()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await SeedSubjectAndCourse(tenantId);
        var s1 = await AddSection(courseId, "S1");
        var s2 = await AddSection(courseId, "S2");

        var i1 = await AddItem(courseId, s1, "Lesson 1");
        var i3 = await AddItem(courseId, s2, "Lesson 3");

        // Cross-section allocation is sequential; each item lands on the mask atomically (same commit)
        i1.BitIndex.Should().Be(0);
        i3.BitIndex.Should().Be(2);

        await ExecuteDbContextAsync(async db =>
        {
            var course = await db.Set<Course>().AsNoTracking().FirstAsync(c => c.Id == courseId);
            BitsShouldBe(course.ActiveItemsBitmask, true, false, true);
        });
    }

    [Fact]
    public async Task DeletedItem_Should_ClearItsBit_PreserveOthers_AndLeaveEnrollmentsUntouched()
    {
        // Arrange: 3 active items, a student completing positions 0 and 1 (≈66.7%)
        var tenantId = Guid.NewGuid();
        var courseId = await SeedSubjectAndCourse(tenantId);
        var s1 = await AddSection(courseId, "S1");
        await AddItem(courseId, s1, "Lesson 1");
        var middle = await AddItem(courseId, s1, "Lesson 2");
        await AddItem(courseId, s1, "Lesson 3");

        var studentId = Guid.NewGuid();
        var enrollmentId = await Enroll(courseId, studentId);

        (await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act: instructor deletes the item at bit index 1
        var deleteResp = await Client.DeleteAsync($"/courses/{courseId}/sections/{s1}/items/{middle.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: course mask is 1,0,1 — position 1 cleared, nothing shifted, length preserved;
        // the enrollment row keeps identical mask bytes + length (zero writes per student).
        await ExecuteDbContextAsync(async db =>
        {
            var course = await db.Set<Course>().AsNoTracking().FirstAsync(c => c.Id == courseId);
            BitsShouldBe(course.ActiveItemsBitmask, true, false, true);

            var persisted = await db.Set<Enrollment>().AsNoTracking().FirstAsync(e => e.Id == enrollmentId);
            BitsShouldBe(persisted.Progress.Bitmask, true, true);
        });

        // Completion derives from the new course mask: numerator 1 (bit 0 only), denominator 2
        var enrollment = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        enrollment!.CompletedItemsCount.Should().Be(1);
        enrollment.ActiveItemsCount.Should().Be(2);
        enrollment.CompletionPercentage.Should().Be(50.0);
    }

    [Fact]
    public async Task CompletingADeactivatedItem_Should_ReturnConflict()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await SeedSubjectAndCourse(tenantId);
        var s1 = await AddSection(courseId);
        var item = await AddItem(courseId, s1, "Lesson 1");
        var enrollmentId = await Enroll(courseId, Guid.NewGuid());

        await Client.DeleteAsync($"/courses/{courseId}/sections/{s1}/items/{item.Id}");

        var completeResp = await Client.PostAsJsonAsync(
            $"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = item.BitIndex });

        completeResp.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "deactivated positions must reject completion writes");
    }

    [Fact]
    public async Task AddingAnItemAfterDeletion_Should_AllocateANeverUsedPosition_AndExtendTheMask()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await SeedSubjectAndCourse(tenantId);
        var s1 = await AddSection(courseId);
        await AddItem(courseId, s1, "First");
        var burned = await AddItem(courseId, s1, "Burned");

        await Client.DeleteAsync($"/courses/{courseId}/sections/{s1}/items/{burned.Id}");

        // The replacement must extend the mask to position 2 — position 1 stays an inactive gap.
        var replacement = await AddItem(courseId, s1, "Replacement");
        replacement.BitIndex.Should().Be(2, because: "burned positions are never reused");

        await ExecuteDbContextAsync(async db =>
        {
            var course = await db.Set<Course>().AsNoTracking().FirstAsync(c => c.Id == courseId);
            BitsShouldBe(course.ActiveItemsBitmask, true, false, true);

            var burnedRow = await db.Set<CurriculumItem>().IgnoreQueryFilters().FirstAsync(i => i.Id == burned.Id);
            burnedRow.IsDeleted.Should().BeTrue();
            burnedRow.BitIndex.Should().Be(1); // historical index retained by the deleted row
        });
    }

    [Fact]
    public async Task DeletingSection_Should_DeactivateAllContainedPositions_InOneCommit()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await SeedSubjectAndCourse(tenantId);
        var s1 = await AddSection(courseId, "S1");
        var s2 = await AddSection(courseId, "S2");
        await AddItem(courseId, s1, "S1 Lesson 1");
        await AddItem(courseId, s1, "S1 Lesson 2");
        await AddItem(courseId, s2, "S2 Lesson");

        var enrollmentId = await Enroll(courseId, Guid.NewGuid());
        (await Client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/complete", new { BitIndex = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var deleteResp = await Client.DeleteAsync($"/courses/{courseId}/sections/{s1}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: bits 0 and 1 deactivated in the section delete's own commit; position 2 intact.
        await ExecuteDbContextAsync(async db =>
        {
            var course = await db.Set<Course>().AsNoTracking().FirstAsync(c => c.Id == courseId);
            BitsShouldBe(course.ActiveItemsBitmask, false, false, true);

            var persistedEnrollment = await db.Set<Enrollment>().AsNoTracking().FirstAsync(e => e.Id == enrollmentId);
            BitsShouldBe(persistedEnrollment.Progress.Bitmask, true); // untouched
        });

        // New denominator = 1 → the surviving completed bit still counts: 100%
        var enrollment = await Client.GetFromJsonAsync<EnrollmentResponse>($"/courses/enrollments/{enrollmentId}");
        enrollment!.CompletedItemsCount.Should().Be(1);
        enrollment.ActiveItemsCount.Should().Be(1);
        enrollment.CompletionPercentage.Should().Be(100.0);
    }

    [Fact]
    public async Task ReorderingItems_Should_NotChangeMasks()
    {
        var tenantId = Guid.NewGuid();
        var courseId = await SeedSubjectAndCourse(tenantId);
        var s1 = await AddSection(courseId);
        var first = await AddItem(courseId, s1, "First");
        var second = await AddItem(courseId, s1, "Second");

        var resp = await Client.PostAsJsonAsync($"/courses/{courseId}/sections/{s1}/reorder",
            new ReorderItemsRequest { CourseId = courseId, SectionId = s1, ItemIds = new() { first.Id, second.Id } });
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ExecuteDbContextAsync(async db =>
        {
            var after = (await db.Set<Course>().AsNoTracking().FirstAsync(c => c.Id == courseId)).ActiveItemsBitmask;
            after.Cast<bool>().Should().BeEquivalentTo(new[] { true, true });
        });
    }
}
