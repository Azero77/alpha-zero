using AlphaZero.Modules.Documents.Application.Sagas;
using AlphaZero.Modules.Documents.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Documents.Infrastructure.Persistance.Configurations;

public class DocumentProcessingSagaStateConfiguration : IEntityTypeConfiguration<DocumentProcessingSagaState>
{
    public void Configure(EntityTypeBuilder<DocumentProcessingSagaState> builder)
    {
        builder.ToTable("DocumentProcessingSagaState", AppDbContext.Schema);

        builder.HasKey(x => x.CorrelationId);
        builder.Property(x => x.CorrelationId).ValueGeneratedNever();

        builder.Property(x => x.CurrentState)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.TenantId)
            .IsRequired();
            
        builder.Property(x => x.UploadTimeoutTokenId);
        builder.Property(x => x.FaultedDeletionTokenId);
        
        builder.Property(x => x.CreatedOn).IsRequired();
        builder.Property(x => x.UpdatedOn);
    }
}
