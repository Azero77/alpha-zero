using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Library.IntegrationEvents;

public class CourseRevocationStrategy(IModuleBus bus, IClock clock) : IRevocationStrategy
{
    public string StrategyId => "enroll-course"; // Matches the activation strategy ID

    public async Task ExecuteAsync(Guid userId, Guid accessCodeId, ResourceArn resource)
    {
        // Publish the revocation event for Courses and Identity modules to consume
        await bus.Publish(new AccessCodeRevokedIntegrationEvent(
            Guid.NewGuid(),
            accessCodeId,
            userId,
            resource,
            clock.Now));
    }
}
