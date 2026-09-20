using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Modules.Courses.IntegrationEvents;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

/// <summary>
/// Handles inter-module playback access verification requests from VideoUploading module.
/// Verifies that the requesting user is actively enrolled in the course,
/// and that the requested video asset is attached to the course/curriculum item.
/// </summary>
public class VerifyCoursePlaybackAccessConsumer : IConsumer<VerifyCoursePlaybackAccessRequest>
{
    private readonly AppDbContext _context;
    private readonly ILogger<VerifyCoursePlaybackAccessConsumer> _logger;

    public VerifyCoursePlaybackAccessConsumer(
        AppDbContext context,
        ILogger<VerifyCoursePlaybackAccessConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VerifyCoursePlaybackAccessRequest> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Verifying course playback access for User: {UserId}, Course: {CourseId}, Video: {VideoId}, Item: {ItemId}",
            msg.UserId, msg.CourseId, msg.VideoId, msg.ItemId);

        // 1. Verify Active Enrollment
        var enrollment = await _context.Enrollements
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.CourseId == msg.CourseId &&
                e.StudentId == msg.UserId &&
                e.Status == EnrollementStatus.Active,
                context.CancellationToken);

        if (enrollment is null)
        {
            _logger.LogWarning("Access denied: User {UserId} is not actively enrolled in Course {CourseId}", msg.UserId, msg.CourseId);
            await context.RespondAsync(new VerifyCoursePlaybackAccessResponse(false, "User is not actively enrolled in this course."));
            return;
        }

        // 2. Verify Video belongs to Course
        var isAssetInCourse = await _context.CourseAssets
            .AsNoTracking()
            .AnyAsync(a => a.CourseId == msg.CourseId && a.Id == msg.VideoId, context.CancellationToken);

        if (!isAssetInCourse)
        {
            _logger.LogWarning("Access denied: Video {VideoId} is not an asset of Course {CourseId}", msg.VideoId, msg.CourseId);
            await context.RespondAsync(new VerifyCoursePlaybackAccessResponse(false, "Video is not associated with this course."));
            return;
        }

        // 3. If ItemId is specified, verify Video belongs to the specific CurriculumItem
        if (msg.ItemId.HasValue)
        {
            var itemExistsWithVideo = await _context.Courses
                .AsNoTracking()
                .Where(c => c.Id == msg.CourseId)
                .SelectMany(c => c.Sections)
                .SelectMany(s => s.Items)
                .Where(i => i.Id == msg.ItemId.Value && !i.IsDeleted)
                .AnyAsync(i => i.Resources.Any(r => r.CourseAssetId == msg.VideoId), context.CancellationToken);

            if (!itemExistsWithVideo)
            {
                _logger.LogWarning("Access denied: Video {VideoId} does not belong to Item {ItemId}", msg.VideoId, msg.ItemId.Value);
                await context.RespondAsync(new VerifyCoursePlaybackAccessResponse(false, "Video does not belong to the specified curriculum item."));
                return;
            }
        }

        _logger.LogInformation("Course playback access granted for User {UserId} to Video {VideoId}", msg.UserId, msg.VideoId);
        await context.RespondAsync(new VerifyCoursePlaybackAccessResponse(true));
    }
}
