namespace AlphaZero.Modules.Courses.Application.Enrollments;

/// <summary>
/// Conventional static permissions for enrollment operations across Courses module.
/// </summary>
public static class EnrollmentPermissions
{
    public const string List = "enrollments:List";
    public const string View = "enrollments:View";
    public const string Complete = "enrollments:Complete";
    public const string Deactivate = "enrollments:Deactivate";
    public const string Suspend = "enrollments:Suspend";
    public const string Reactivate = "enrollments:Reactivate";
}
