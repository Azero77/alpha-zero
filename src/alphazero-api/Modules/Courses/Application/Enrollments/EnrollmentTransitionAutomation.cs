namespace AlphaZero.Modules.Courses.Application.Enrollments;

/// <summary>
/// Transport claim for automated lifecycle transitions (issue #34 story #10 §3.5): automation
/// paths (bus consumers) dispatch their commands OUTSIDE any HTTP actor context, so without a
/// claim they are indistinguishable from a rogue in-process send — and story #10 requires the
/// rogue sends to be refused before the aggregate is touched. The claim closes that gap: the
/// revocation saga consumer wraps its module dispatch in <see cref="ClaimSagaTransport"/> and the
/// actor guard honours ONLY that explicit, transport-claimed signal.
///
/// The claim is <see cref="AsyncLocal"/>: the consumer's <c>coursesModule.Send</c> creates a fresh
/// request scope ( AppModule.Send), so no scoped service could carry the signal — but AsyncLocal
/// flows DOWN into the awaited MediatR pipeline and stops at the claim's disposal. Sync frames on
/// the caller's chain never see the value (the set/dispose both happen in the consumer).
/// </summary>
public static class EnrollmentTransitionAutomation
{
    private static readonly AsyncLocal<bool> Claimed = new();

    public static bool HasTransportClaim() => Claimed.Value;

    /// <summary>
    /// Claims the transport origin. Dispose the token exactly when the dispatch is done —
    /// wrap the <c>Send</c> call in a using block.
    /// </summary>
    public static SagaTransportClaim ClaimSagaTransport()
    {
        Claimed.Value = true;
        return new SagaTransportClaim();
    }

    public sealed class SagaTransportClaim : IDisposable
    {
        public void Dispose() => Claimed.Value = false;
    }
}
