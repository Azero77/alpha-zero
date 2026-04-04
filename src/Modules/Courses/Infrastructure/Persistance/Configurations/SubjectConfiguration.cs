using AlphaZero.Modules.Courses.Domain.Aggregates.Subject;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

public class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(s => s.Description)
            .HasMaxLength(1024);
    }
}
