using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Infrastructure.Persistance;
using AlphaZero.Shared.Application;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Infrastructure.Analytics;

public interface IAccessCodeExpirySweepService
{
    Task<int> SweepExpiredCodesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Bulk expiry sweep (issue #59 story #5). Explicit deviation from the Courses expiry precedent
/// (<c>EnrollmentExpiryService</c> uses one set-based UPDATE): the AccessCode state machine is the
/// only sanctioned mutation path (invariant I-STATE), so this sweep mass-loads due codes with a
/// deterministic FOR UPDATE lock (ordered by "Id" to prevent deadlocks), transitions each through
/// <c>AccessCode.Expire()</c>, and persists via the conditional tripwire
/// (<c>IAccessCodeRepository.UpdateAsync</c>). Domain events flow through the Unit of Work, so each
/// expiry is projected into the append-only audit stream exactly like interactive transitions.
/// Lock-order discipline (mirrors RedeemCodeCommandHandler): AccessCode rows are the only locks
/// taken here, acquired in "Id" order; the transaction (audit stream inserts + code updates)
/// commits after SaveChangesAsync.
/// </summary>
public class AccessCodeExpirySweepService(
    AppDbContext context,
    IAccessCodeRepository accessCodeRepository,
    IUnitOfWork unitOfWork,
    ILogger<AccessCodeExpirySweepService> logger) : IAccessCodeExpirySweepService
{
    public async Task<int> SweepExpiredCodesAsync(CancellationToken cancellationToken = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        var expired = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var now = DateTime.UtcNow;

            // FOR UPDATE ... ORDER BY "Id" (documented lock order), via the repository's guarded
            // mass-load (covers all tenants — the sweep runs without a request tenant context).
            var dueCodes = await accessCodeRepository.GetDueForExpiryAsync(now, cancellationToken);

            if (dueCodes.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return 0;
            }

            var successCount = 0;
            foreach (var code in dueCodes)
            {
                var expireResult = code.Expire(now);
                if (expireResult.IsError)
                {
                    // Should be unreachable given the status filter; log loudly and defer to the next pass.
                    logger.LogWarning(
                        "Expiry sweep skipped code {CodeId} (status {Status}): {Error}",
                        code.Id, code.Status, string.Join(", ", expireResult.Errors.Select(e => e.Description)));
                    continue;
                }

                var updateResult = await accessCodeRepository.UpdateAsync(code, cancellationToken);
                if (updateResult.IsError)
                {
                    // Tripwire fired: concurrent modification win detected — defer that code to the next pass.
                    logger.LogWarning(
                        "Expiry sweep defer for code {CodeId}: {Error}",
                        code.Id, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                    continue;
                }

                successCount++;
            }

            // UnitOfWork dispatches the AccessCodeExpiredDomainEvents (→ audit stream projection)
            // and saves everything in this same transaction.
            await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return successCount;
        });

        if (expired > 0)
        {
            logger.LogInformation("Expiry sweep transitioned {Count} access codes to Expired.", expired);
        }

        return expired;
    }
}
