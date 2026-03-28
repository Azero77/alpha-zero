using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

}
