using System.Security.Cryptography;
using System.Text;

namespace AlphaZero.Modules.Identity.Application.Principals.Commands.EnsureCoursePlanPrincipal;

/// <summary>
/// The deterministic naming contract of the per-(tenant, course, plan) role principal (issue #34
/// rework §3.2.1, step 9). Derived ids make <see cref="EnsureCoursePlanPrincipalCommandHandler"/>
/// idempotent: re-running over the same (course, plan) — or after a partial failure where Identity
/// committed but the Courses plan write rolled back — re-finds the principal instead of minting
/// twins. A UUIDv5-style hash keeps the cross-module caller unambiguous without a lookup table.
/// </summary>
public sealed record CoursePlanPrincipalFacts(Guid TenantId, Guid CourseId, Guid PlanId)
{
    /// <summary>Fixed name-based (RFC 4122 §6.5) namespace for course-plan principal ids.</summary>
    private static readonly Guid Namespace = Guid.Parse("d4f7a2e1-8c3b-4d5e-9a1f-2c6b3d7e8f9a");

    public Guid PrincipalId => UuidV5(Namespace, $"{TenantId}|{CourseId}|{PlanId}");

    public Guid PolicyId => UuidV5(Namespace, $"{TenantId}|{CourseId}|{PlanId}|policy");

    /// <summary>Unique per (tenant, course, plan); fits the 128-char username column.</summary>
    public string Username => $"course-plan-{CourseId}-{PlanId}"[0..Math.Min(128, $"course-plan-{CourseId}-{PlanId}".Length)];

    /// <summary>Stable unique-name key for the globally-unique managed-policy name index.</summary>
    public string PolicyName => $"CourseAccess-{CourseId}-{PlanId}"[0..Math.Min(100, $"CourseAccess-{CourseId}-{PlanId}".Length)];

    public string DisplayName => $"Course access plan {PlanId}";

    /// <summary>RFC 4122 name-based (UUIDv5, SHA-1) generation over <paramref name="name"/>.</summary>
    private static Guid UuidV5(Guid ns, string name)
    {
        var nsBytes = ns.ToByteArray();
        var nameBytes = Encoding.UTF8.GetBytes(name);

        var input = new byte[nsBytes.Length + nameBytes.Length];
        nsBytes.CopyTo(input, 0);
        nameBytes.CopyTo(input, nsBytes.Length);

        var hash = SHA1.HashData(input);

        Span<byte> bytes = hash.AsSpan(0, 16);
        bytes[6] = (byte)((bytes[6] & 0x0F) | (5 << 4)); // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);     // RFC 4122 variant

        return new Guid(bytes);
    }
}
