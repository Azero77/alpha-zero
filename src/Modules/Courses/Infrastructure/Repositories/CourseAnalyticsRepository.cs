using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Infrastructure.Repositories;

public class CourseAnalyticsRepository : ICourseAnalyticsRepository
{
    private readonly AppDbContext _context;

    public CourseAnalyticsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task IncrementItemCompletionAsync(Guid courseId, int bitIndex, double diff, CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Courses"".""CourseAnalytics""
            SET 
                ""SumOfCompletionPercentages"" = ""SumOfCompletionPercentages"" + {diff},
                ""ItemCompletions"" = jsonb_set(
                    COALESCE(""ItemCompletions"", '{{}}'::jsonb),
                    {{{bitIndex.ToString()}}}, 
                    (COALESCE((""ItemCompletions""->>{bitIndex.ToString()})::int, 0) + 1)::text::jsonb
                )
            WHERE ""CourseId"" = {courseId}", cancellationToken);
    }

    public async Task IncrementEnrollmentCountAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO ""Courses"".""CourseAnalytics"" (""CourseId"", ""TotalEnrollments"", ""SumOfCompletionPercentages"", ""ItemCompletions"")
            VALUES ({courseId}, 1, 0, '{{}}'::jsonb)
            ON CONFLICT (""CourseId"") 
            DO UPDATE SET ""TotalEnrollments"" = ""CourseAnalytics"".""TotalEnrollments"" + 1", cancellationToken);
    }
}
