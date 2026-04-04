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
            
            bool isSoftDelete = typeof(ISoftDeleteItem).IsAssignableFrom(entityClrType);
            bool isTenantOwned = typeof(IDomainTenantOwned).IsAssignableFrom(entityClrType);

            if (!isSoftDelete && !isTenantOwned) continue;

            var parameter = Expression.Parameter(entityClrType, "e");
            Expression? combinedExpression = null;

            // 1. Soft Delete Filter: e.IsDeleted == false
            if (isSoftDelete)
            {
                var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeleteItem.IsDeleted));
                var falseConstant = Expression.Constant(false);
                combinedExpression = Expression.Equal(isDeletedProperty, falseConstant);
            }

            // 2. Tenant Filter: e.TenantId == tenantProvider.GetTenant()
            if (isTenantOwned)
            {
                var tenantIdProperty = Expression.Property(parameter, nameof(IDomainTenantOwned.TenantId));
                
                // This creates: () => tenantProvider.GetTenant()
                Expression<Func<Guid?>> tenantProviderExpression = () => tenantProvider.GetTenant();
                var tenantIdComparison = Expression.Equal(tenantIdProperty, tenantProviderExpression.Body);

                combinedExpression = combinedExpression == null 
                    ? tenantIdComparison 
                    : Expression.AndAlso(combinedExpression, tenantIdComparison);
            }

            if (combinedExpression != null)
            {
                var lambda = Expression.Lambda(combinedExpression, parameter);
                modelBuilder.Entity(entityClrType).HasQueryFilter(lambda);
            }
        }
    }
}
