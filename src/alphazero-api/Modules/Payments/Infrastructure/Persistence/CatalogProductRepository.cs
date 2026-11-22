using AlphaZero.Modules.Payments.Application.Catalog;
using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.Domain.Ledger;
using AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence;

public sealed class CatalogProductRepository(AppDbContext context) : ICatalogProductRepository
{
    public async Task<Product?> FindBySourceIdentityAsync(Guid tenantId, string itemType, Guid itemId,
        CancellationToken cancellationToken) =>
        await context.Products.SingleOrDefaultAsync(product => product.TenantId == tenantId
            && product.ItemType == itemType && product.ItemId == itemId, cancellationToken) is { } product
                ? ToDomain(product)
                : null;

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        var model = new ProductDataModel
        {
            Id = product.Id,
            TenantId = product.TenantId,
            ItemType = product.ItemType,
            ItemId = product.ItemId,
            ParentContainerId = product.ParentContainerId,
            TargetResourceArn = product.TargetResourceArn,
            Title = product.Title,
            SourceAvailability = product.SourceAvailability.ToString(),
            SourceVersion = product.SourceVersion,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
        await context.Products.AddAsync(model, cancellationToken);
    }

    public Task UpdateAsync(Product product, CancellationToken cancellationToken)
    {
        var model = context.Products.Local.SingleOrDefault(candidate => candidate.Id == product.Id);
        if (model is null)
        {
            model = new ProductDataModel { Id = product.Id };
            context.Products.Attach(model);
        }
        model.TenantId = product.TenantId;
        model.ItemType = product.ItemType;
        model.ItemId = product.ItemId;
        model.ParentContainerId = product.ParentContainerId;
        model.TargetResourceArn = product.TargetResourceArn;
        model.Title = product.Title;
        model.SourceAvailability = product.SourceAvailability.ToString();
        model.SourceVersion = product.SourceVersion;
        model.CreatedAt = product.CreatedAt;
        model.UpdatedAt = product.UpdatedAt;
        context.Entry(model).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);

    public async Task<int> ApplyCatalogSnapshotAsync(IReadOnlyList<CatalogProductSnapshot> sourceSnapshot,
        CancellationToken cancellationToken)
    {
        var applied = 0;
        foreach (var source in sourceSnapshot)
        {
            var existing = await FindBySourceIdentityAsync(source.TenantId, "CoursePlan", source.CoursePlanId, cancellationToken);
            if (existing is null)
            {
                var created = Product.Create(source.CoursePlanId, source.TenantId, "CoursePlan", source.CoursePlanId,
                    source.CourseId, source.TargetResourceArn, source.Title, source.Availability, source.SourceVersion, source.OccurredAt);
                if (created.IsError) throw new InvalidOperationException(string.Join("; ", created.Errors.Select(error => error.Description)));
                await AddAsync(created.Value, cancellationToken);
                applied++;
                continue;
            }
            var result = existing.ApplyMirrorUpdate(source.SourceVersion, source.Title, source.Availability,
                source.TargetResourceArn, source.OccurredAt);
            if (result.IsError) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
            if (!result.Value) continue;
            await UpdateAsync(existing, cancellationToken);
            applied++;
        }
        return applied;
    }

    public async Task<IReadOnlyList<Product>> GetCatalogSnapshotAsync(CancellationToken cancellationToken)
    {
        var rows = await context.Products.AsNoTracking()
            .OrderBy(product => product.TenantId).ThenBy(product => product.ItemId)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDomain).ToArray();
    }

    private static Product ToDomain(ProductDataModel model) => Product.Reconstitute(model.Id, model.TenantId,
        model.ItemType, model.ItemId, model.ParentContainerId, model.TargetResourceArn, model.Title,
        Enum.Parse<SourceAvailability>(model.SourceAvailability), model.SourceVersion, model.CreatedAt, model.UpdatedAt);

}
