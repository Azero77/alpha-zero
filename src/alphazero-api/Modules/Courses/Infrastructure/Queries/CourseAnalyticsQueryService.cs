using AlphaZero.Modules.Courses.Application.Analytics.Queries.GetCourseAnalytics;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Infrastructure.Queries;

public class CourseAnalyticsQueryService : ICourseAnalyticsQueryService
{
    private readonly AppDbContext _context;

    public CourseAnalyticsQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CourseAnalyticsDto?> GetCourseAnalyticsAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var analytics = await _context.CourseAnalytics
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CourseId == courseId, cancellationToken);

        if (analytics == null)
            return null;

        var avgCompletion = analytics.TotalEnrollments > 0 
            ? Math.Round(analytics.SumOfCompletionPercentages / analytics.TotalEnrollments, 2) 
            : 0;

        var itemRates = new List<ItemCompletionDto>();
        if (analytics.ItemCompletions != null)
        {
            foreach (var kvp in analytics.ItemCompletions)
            {
                double itemAvg = analytics.TotalEnrollments > 0 
                    ? Math.Round((double)kvp.Value / analytics.TotalEnrollments * 100, 2) 
                    : 0;

                itemRates.Add(new ItemCompletionDto(kvp.Key, kvp.Value, itemAvg));
            }
        }

        return new CourseAnalyticsDto(
            courseId,
            analytics.TotalEnrollments,
            avgCompletion,
            itemRates.OrderBy(x => x.BitIndex).ToList()
        );
    }
}
