using AlphaZero.Shared.Authorization;

namespace AlphaZero.Modules.Courses.Infrastructure.Authorization;

public class CourseArnResolver : IArnResolver
{
    public Task<bool> IsResourceInContainerAsync(Guid containerId, Guid resourceId, CancellationToken ct = default)
    {
        // User will implement ARN resolution logic
        throw new NotImplementedException();
    }
}
