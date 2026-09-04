using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Shared.Infrastructure.Tenats;

public class TenantOwnedEntityConfiguration :
    IEntityTypeConfiguration<TenantOwned>

{
    private readonly ITenantProvider _tenantProvider;

    public TenantOwnedEntityConfiguration(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public void Configure(EntityTypeBuilder<TenantOwned> builder)
    {
        Guid? tenant = _tenantProvider.GetTenant();
        builder.HasQueryFilter(t => t.TenantId == tenant);
    }
}
