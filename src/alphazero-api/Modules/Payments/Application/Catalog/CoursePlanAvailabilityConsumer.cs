using AlphaZero.Modules.Payments.Domain.Catalog;

namespace AlphaZero.Modules.Payments.Application.Catalog;

/// <summary>Idempotent, source-version guarded consumer; replay and reordering are safe.</summary>
public sealed class CoursePlanAvailabilityConsumer(ICatalogProductRepository products)
{
    public async Task Consume(CatalogProductSnapshot message, CancellationToken cancellationToken = default)
    {
        var product = await products.FindBySourceIdentityAsync(message.TenantId, "CoursePlan", message.CoursePlanId, cancellationToken);
        if (product is null)
        {
            var created = Product.Create(message.CoursePlanId, message.TenantId, "CoursePlan", message.CoursePlanId,
                message.CourseId, message.TargetResourceArn, message.Title,
                message.Availability,
                message.SourceVersion, message.OccurredAt);
            if (created.IsError) throw new InvalidOperationException(string.Join("; ", created.Errors.Select(error => error.Description)));
            await products.AddAsync(created.Value, cancellationToken);
        }
        else
        {
            var update = product.ApplyMirrorUpdate(message.SourceVersion, message.Title,
                message.Availability,
                message.TargetResourceArn, message.OccurredAt);
            if (update.IsError) throw new InvalidOperationException(string.Join("; ", update.Errors.Select(error => error.Description)));
            if (!update.Value) return;
            await products.UpdateAsync(product, cancellationToken);
        }
        await products.SaveChangesAsync(cancellationToken);
    }
}

public sealed record CatalogProductSnapshot(Guid TenantId, Guid CourseId, Guid CoursePlanId,
    string TargetResourceArn, string Title, SourceAvailability Availability, long SourceVersion, DateTime OccurredAt);
