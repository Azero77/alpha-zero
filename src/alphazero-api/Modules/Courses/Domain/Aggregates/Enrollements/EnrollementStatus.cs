namespace AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;

public enum EnrollementStatus
{
    /// <summary>
    /// Student has full access to course materials and can track progress.
    /// </summary>
    Active,

    /// <summary>
    /// Access is disabled by choice or standard operation (e.g., student unenrolled).
    /// Student can usually re-enroll if the course is available.
    /// </summary>
    Inactive,

    /// <summary>
    /// Access is forcibly blocked due to a violation (e.g., account sharing, payment dispute).
    /// Student cannot re-enroll and must contact support.
    /// </summary>
    Suspended,

    /// <summary>
    /// The enrollment period has naturally ended. Access is typically read-only or 
    /// requires a new purchase/code redemption.
    /// </summary>
    Expired
}
