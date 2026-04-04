using System.Linq.Expressions;
using System.Reflection;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Shared.Infrastructure.Database;

public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies global query filters for Soft Delete (ISoftDeleteItem) and Multi-Tenancy (IDomainTenantOwned).
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="tenantProvider">The tenant provider to retrieve the current tenant context.</param>
    public static void ApplyAlphaZeroGlobalFilters(this ModelBuilder modelBuilder, ITenantProvider tenantProvider)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Only apply filters to non-owned types and root entities
            if (entityType.BaseType != null || entityType.IsOwned()) continue;

            var entityClrType = entityType.ClrType;
            
            bool isSoftDelete = typeof(ISoftDeletable).IsAssignableFrom(entityClrType);
            bool isTenantOwned = typeof(IDomainTenantOwned).IsAssignableFrom(entityClrType);

            if (!isSoftDelete && !isTenantOwned) continue;

            var parameter = Expression.Parameter(entityClrType, "e");
            Expression? combinedExpression = null;

            // 1. Soft Delete Filter: e.IsDeleted == false
            if (isSoftDelete)
            {
                var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var falseConstant = Expression.Constant(false);
                combinedExpression = Expression.Equal(isDeletedProperty, falseConstant);

                // PERFORMANCE: Add a filtered (partial) index for non-deleted items
                // This makes the Global Query Filter extremely fast.
                modelBuilder.Entity(entityClrType)
                    .HasIndex(nameof(ISoftDeletable.IsDeleted))
                    .HasFilter("\"IsDeleted\" = FALSE"); 
            }

            // 2. Tenant Filter: e.TenantId == tenantProvider.GetTenant()
            if (isTenantOwned)
            {
                var tenantIdProperty = Expression.Property(parameter, nameof(IDomainTenantOwned.TenantId));
                
                // PERFORMANCE: Ensure types match for the Equal operator (Guid vs Guid?)
                var tenantIdPropertyConverted = Expression.Convert(tenantIdProperty, typeof(Guid?));
                
                // This creates: () => tenantProvider.GetTenant()
                Expression<Func<Guid?>> tenantProviderExpression = () => tenantProvider.GetTenant();
                var tenantIdComparison = Expression.Equal(tenantIdPropertyConverted, tenantProviderExpression.Body);

                combinedExpression = combinedExpression == null 
                    ? tenantIdComparison 
                    : Expression.AndAlso(combinedExpression, tenantIdComparison);

                // PERFORMANCE: Add an index for TenantId
                // If soft-delete is also present, we make it a partial index for even better performance
                var tenantIndex = modelBuilder.Entity(entityClrType)
                    .HasIndex(nameof(IDomainTenantOwned.TenantId));

                if (isSoftDelete)
                {
                    tenantIndex.HasFilter("\"IsDeleted\" = FALSE");
                }
            }

            if (combinedExpression != null)
            {
                var lambda = Expression.Lambda(combinedExpression, parameter);
                modelBuilder.Entity(entityClrType).HasQueryFilter(lambda);
            }
        }
    }
}
