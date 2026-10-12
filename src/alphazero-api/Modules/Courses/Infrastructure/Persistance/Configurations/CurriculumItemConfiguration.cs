using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

public class CurriculumItemConfiguration : IEntityTypeConfiguration<CurriculumItem>
{
    public void Configure(EntityTypeBuilder<CurriculumItem> builder)
    {
        builder.ToTable("CurriculumItems", "Courses");
        builder.HasKey(i => i.Id);
        
        builder.Property(i => i.Id)
            .ValueGeneratedNever();

        builder.Property(i => i.Title)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(i => i.MainType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.BitIndex)
            .IsRequired();

        builder.Property(i => i.Order)
            .IsRequired();

        builder.HasMany(i => i.Resources)
            .WithOne()
            .HasForeignKey(r => r.CurriculumItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.SectionId);
    }
}
