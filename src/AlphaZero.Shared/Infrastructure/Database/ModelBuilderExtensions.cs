using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Shared.Infrastructure.Database;

public class DbContstants
{
    public const string SoftDeleteFilter = "SoftDelete";
    public const string TenantFilter = "Tenant";
}

public static class ModelBuilderExtensions
{

    /// <summary>
    /// Applies global query filters for Soft Delete (ISoftDeleteItem) and Multi-Tenancy (IDomainTenantOwned).
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="context">The tenant context to retrieve the current tenant context.</param>
    public static void ApplyAlphaZeroGlobalFilters(this ModelBuilder modelBuilder, ITenantDbContext context)
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

            // 1. Soft Delete Filter: e.IsDeleted == false
            if (isSoftDelete)
            {
                var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var falseConstant = Expression.Constant(false);
                var isDeletedExpression = Expression.Equal(isDeletedProperty, falseConstant);
                var lambda = Expression.Lambda(isDeletedExpression,parameter);
                
                modelBuilder.Entity(entityClrType)
                    .HasIndex(nameof(ISoftDeletable.IsDeleted))
                    .HasFilter("\"IsDeleted\" = FALSE");
                modelBuilder.Entity(entityClrType)
                    .HasQueryFilter(DbContstants.SoftDeleteFilter, lambda);
            }

            // 2. Tenant Filter: e.TenantId == context.TenantId
            if (isTenantOwned)
            {
                var tenantProp = Expression.Property(parameter, nameof(IDomainTenantOwned.TenantId));
                var tenantPropNullable = Expression.Convert(tenantProp, typeof(Guid?));

                // Correctly capture the ITenantDbContext instance. 
                // EF Core will swap this constant with the current active DbContext at runtime.
                var contextConstant = Expression.Constant(context);
                var tenantIdFromContext = Expression.Property(contextConstant, nameof(ITenantDbContext.TenantId));

                var tenantEqual = Expression.Equal(tenantPropNullable, tenantIdFromContext);
                var lambda = Expression.Lambda(tenantEqual, parameter);

                var tenantIndex = modelBuilder.Entity(entityClrType)
                    .HasIndex(nameof(IDomainTenantOwned.TenantId));

                if (isSoftDelete)
                    tenantIndex.HasFilter("\"IsDeleted\" = FALSE");

                modelBuilder.Entity(entityClrType)
                    .HasQueryFilter(DbContstants.TenantFilter, lambda);
            }
        }
    }
}
