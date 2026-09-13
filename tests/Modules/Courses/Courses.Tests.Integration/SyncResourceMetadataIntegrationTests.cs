using System.Text.Json;
using AlphaZero.Modules.Courses.Application;
using AlphaZero.Modules.Courses.Application.Courses.Commands.SyncResourceMetadata;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Courses.Tests.Integration;

public class SyncResourceMetadataIntegrationTests : BaseIntegrationTest
{
    public SyncResourceMetadataIntegrationTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedCourse(Guid tenantId)
    {
        var subject = AlphaZero.Modules.Courses.Domain.Aggregates.Subject.Subject.Create(
            Guid.NewGuid(), tenantId, "CS", "Computer Science").Value;
        DbContext.Subjects.Add(subject);
        await DbContext.SaveChangesAsync();

        var course = Course.Create(Guid.NewGuid(), tenantId, "Test Course", "Desc", subject.Id).Value;
        DbContext.Courses.Add(course);
        await DbContext.SaveChangesAsync();

        return course.Id;
    }

    [Fact]
    public async Task SyncResourceMetadata_Should_UpdateVideoStreamingInfo_InDatabase()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseId = await SeedCourse(tenantId);

        var videoId = Guid.NewGuid();
        var videoArn = ResourceArn.ForVideo(tenantId, videoId);
        var videoAsset = VideoCourseAsset.Create(videoId, tenantId, videoArn, "Old Video Title", courseId).Value;
        DbContext.CourseAssets.Add(videoAsset);
        await DbContext.SaveChangesAsync();

        using var scope = Factory.Services.CreateScope();
        var coursesModule = scope.ServiceProvider.GetRequiredService<ICoursesModule>();

        var payload = JsonDocument.Parse("""
        {
            "title": "Synchronized Video Title",
            "thumbnailUrl": "https://cdn.alphazero.com/thumbs/vid1.png",
            "relativeStreamingUrl": "streaming/vid1/master.m3u8",
            "duration": "00:42:15"
        }
        """).RootElement;

        var command = new SyncResourceMetadataCommand(videoId, payload);

        // Act
        var result = await coursesModule.Send(command);

        // Assert
        result.IsError.Should().BeFalse();

        // Re-query from DB
        var updatedAsset = await DbContext.CourseAssets
            .OfType<VideoCourseAsset>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == videoId);

        updatedAsset.Should().NotBeNull();
        updatedAsset!.Title.Should().Be("Synchronized Video Title");
        updatedAsset.ThumbnailUrl.Should().Be("https://cdn.alphazero.com/thumbs/vid1.png");
        updatedAsset.RelativeStreamingUrl.Should().Be("streaming/vid1/master.m3u8");
        updatedAsset.Duration.Should().Be(TimeSpan.FromMinutes(42).Add(TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public async Task SyncResourceMetadata_Should_UpdateDocumentFileInfo_InDatabase()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseId = await SeedCourse(tenantId);

        var docId = Guid.NewGuid();
        var docArn = ResourceArn.ForDocument(tenantId, docId);
        var docAsset = DocumentCourseAsset.Create(
            docId, tenantId, docArn, "Initial Doc Title", courseId, "draft.pdf", 1024, "application/pdf").Value;
        DbContext.CourseAssets.Add(docAsset);
        await DbContext.SaveChangesAsync();

        using var scope = Factory.Services.CreateScope();
        var coursesModule = scope.ServiceProvider.GetRequiredService<ICoursesModule>();

        var payload = JsonDocument.Parse("""
        {
            "title": "Final Lecture Notes",
            "fileName": "lecture_notes_v3.pdf",
            "fileSize": 10485760,
            "contentType": "application/pdf"
        }
        """).RootElement;

        var command = new SyncResourceMetadataCommand(docId, payload);

        // Act
        var result = await coursesModule.Send(command);

        // Assert
        result.IsError.Should().BeFalse();

        var updatedAsset = await DbContext.CourseAssets
            .OfType<DocumentCourseAsset>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == docId);

        updatedAsset.Should().NotBeNull();
        updatedAsset!.Title.Should().Be("Final Lecture Notes");
        updatedAsset.FileName.Should().Be("lecture_notes_v3.pdf");
        updatedAsset.Size.Should().Be(10485760);
        updatedAsset.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task SyncResourceMetadata_Should_UpdateAssessmentInfo_InDatabase()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseId = await SeedCourse(tenantId);

        var assessmentId = Guid.NewGuid();
        var assessmentArn = ResourceArn.ForAssessment(tenantId, assessmentId);
        var assessmentAsset = AssessmentCourseAsset.Create(
            assessmentId, tenantId, assessmentArn, "Initial Quiz Title", courseId, 5, AssessmentType.Practice).Value;
        DbContext.CourseAssets.Add(assessmentAsset);
        await DbContext.SaveChangesAsync();

        using var scope = Factory.Services.CreateScope();
        var coursesModule = scope.ServiceProvider.GetRequiredService<ICoursesModule>();

        var payload = JsonDocument.Parse("""
        {
            "title": "Semester Midterm Exam",
            "questionsNumber": 40,
            "type": "Midterm"
        }
        """).RootElement;

        var command = new SyncResourceMetadataCommand(assessmentId, payload);

        // Act
        var result = await coursesModule.Send(command);

        // Assert
        result.IsError.Should().BeFalse();

        var updatedAsset = await DbContext.CourseAssets
            .OfType<AssessmentCourseAsset>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assessmentId);

        updatedAsset.Should().NotBeNull();
        updatedAsset!.Title.Should().Be("Semester Midterm Exam");
        updatedAsset.QuestionsNumber.Should().Be(40);
        updatedAsset.Type.Should().Be(AssessmentType.Midterm);
    }
}
