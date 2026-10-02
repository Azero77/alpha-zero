namespace AlphaZero.Modules.Courses.Infrastructure;


public class CourseAnalytics
{
    public Guid CourseId { get; private set; }
    public int TotalEnrollments { get; private set; }
    public double SumOfCompletionPercentages { get; private set; }
    public Dictionary<int, int> ItemCompletions { get; private set; }

    private CourseAnalytics()
    {
        ItemCompletions = new Dictionary<int, int>();
    }

    private CourseAnalytics(Guid courseId)
    {
        CourseId = courseId;
        TotalEnrollments = 0;
        SumOfCompletionPercentages = 0;
        ItemCompletions = new Dictionary<int, int>();
    }

    public static CourseAnalytics Create(Guid courseId)
    {
        return new CourseAnalytics(courseId);
    }
}
