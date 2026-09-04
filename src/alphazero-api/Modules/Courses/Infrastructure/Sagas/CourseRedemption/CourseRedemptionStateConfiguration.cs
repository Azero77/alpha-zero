using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Courses.Infrastructure.Sagas.CourseRedemption;

public class CourseRedemptionStateConfiguration : IEntityTypeConfiguration<CourseRedemptionState>
{
    public void Configure(EntityTypeBuilder<CourseRedemptionState> builder)
    {
        builder.HasKey(x => x.CorrelationId);
        builder.Property(x => x.CurrentState).IsRequired().HasMaxLength(64);
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.AccessCodeId).IsRequired();
        builder.Property(x => x.CourseArn).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Plan).HasMaxLength(64);
    }
}
