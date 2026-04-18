using System.Text.Json;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Application;

namespace AlphaZero.Modules.Library.IntegrationEvents;

/// <summary>
/// 🛡️ ANTI-CORRUPTION LAYER (ACL)
/// This strategy lives in the IntegrationEvents layer to serve as a bridge between modules.
/// It translates the Library's generic redemption metadata into specific Course integration events.
/// By doing this, the Library Domain remains pure and unaware of Course-specific logic.
/// </summary>
public class CourseEnrollmentStrategy : IRedemptionStrategy
{
    public string StrategyId => "enroll-course";
    private readonly IModuleBus _bus;

    public CourseEnrollmentStrategy(IModuleBus bus)
    {
        _bus = bus;
    }

    public async Task ExecuteAsync(Guid userId, ResourceArn resource, JsonElement metadata)
    {
        // Translation Logic (ACL):
        string plan = "Standard";
        if (metadata.TryGetProperty("plan", out var planProp))
        {
            plan = planProp.GetString() ?? "Standard";
        }

        // Fire the integration event defined by the target module (Courses)
        await _bus.Publish(new CourseAccessUnlockedIntegrationEvent(
            userId, 
            resource, 
            plan));
    }
}
