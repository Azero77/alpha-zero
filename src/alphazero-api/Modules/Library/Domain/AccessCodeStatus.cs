using System.ComponentModel;

namespace AlphaZero.Modules.Library.Domain;

/// <summary>
/// Defines the lifecycle stages of an Access Code within the physical economy.
/// </summary>
public enum AccessCodeStatus
{
    /// <summary>
    /// The code has been generated and hashed in the database but is not yet available at a physical branch.
    /// </summary>
    [Description("Code has been generated but not yet distributed to a library.")]
    Minted = 1,

    /// <summary>
    /// The code has been physically sent to a library branch and is ready for sale to students.
    /// </summary>
    [Description("Code is active and available at a library for sale.")]
    Distributed = 2,

    /// <summary>
    /// The code has been used by a student to unlock a resource. It is no longer valid for further redemptions.
    /// </summary>
    [Description("Code has already been used by a student.")]
    Redeemed = 3,

    /// <summary>
    /// The code has been manually revoked by an administrator (e.g., due to theft, non-payment, or error).
    /// </summary>
    [Description("Code has been revoked and is no longer valid.")]
    Voided = 4
}
