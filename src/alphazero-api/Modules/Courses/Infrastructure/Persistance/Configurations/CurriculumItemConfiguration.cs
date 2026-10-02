using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
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

        builder.OwnsMany(i => i.Resources, rb =>
        {
            rb.ToTable("CurriculumResources", "Courses");
            rb.WithOwner().HasForeignKey("CurriculumItemId");
            rb.Property<Guid>("Id").ValueGeneratedOnAdd();
            rb.HasKey("Id");

            rb.Property(r => r.CourseAssetId).IsRequired();
            rb.HasOne(r => r.Asset)
                .WithMany()
                .HasForeignKey(r => r.CourseAssetId)
                .OnDelete(DeleteBehavior.Restrict);

            rb.Property(r => r.Order)
                .IsRequired();

            rb.Property(r => r.Metadata)
                .HasColumnType("jsonb");
        });
    }
}
