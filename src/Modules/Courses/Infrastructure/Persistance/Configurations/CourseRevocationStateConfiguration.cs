using AlphaZero.Modules.Courses.Infrastructure.Sagas.CourseRevocation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

public class CourseRevocationStateConfiguration : IEntityTypeConfiguration<CourseRevocationState>
{
    public void Configure(EntityTypeBuilder<CourseRevocationState> builder)
    {
        builder.HasKey(cs => cs.CorrelationId);
    }
}