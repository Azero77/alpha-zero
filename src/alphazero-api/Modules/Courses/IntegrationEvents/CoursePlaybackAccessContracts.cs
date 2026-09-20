namespace AlphaZero.Modules.Courses.IntegrationEvents;

/// <summary>
/// Inter-module query sent by VideoUploading module to Courses module
/// to verify student enrollment and curriculum item access prerequisites.
/// </summary>
public record VerifyCoursePlaybackAccessRequest(
    Guid CourseId,
    Guid? ItemId,
    Guid VideoId,
    Guid UserId);

public record VerifyCoursePlaybackAccessResponse(
    bool IsAllowed,
    string? Reason = null);
