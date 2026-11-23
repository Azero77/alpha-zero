using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Payments.Application.Catalog;

public sealed record CatalogProductSnapshot(
    Guid TenantId,
    string ItemType,
    Guid ItemId,
    Guid? ParentContainerId,
    string TargetResourceArn,
    string Title,
    SourceAvailability Availability,
    long SourceVersion,
    DateTime OccurredAt)
{
    public CatalogProductSnapshot(
        Guid tenantId,
        Guid courseId,
        Guid coursePlanId,
        string targetResourceArn,
        string title,
        SourceAvailability availability,
        long sourceVersion,
        DateTime occurredAt)
        : this(tenantId, "CoursePlan", coursePlanId, courseId, targetResourceArn, title, availability, sourceVersion, occurredAt)
    {
    }
}

public sealed record ReconcileProductCatalog(IReadOnlyList<CatalogProductSnapshot> SourceSnapshot)
    : ICommand<int>, ITransactionalCommand;

public sealed class ReconcileProductCatalogHandler(ICatalogProductRepository products)
    : IRequestHandler<ReconcileProductCatalog, ErrorOr<int>>
{
    public async Task<ErrorOr<int>> Handle(ReconcileProductCatalog request, CancellationToken cancellationToken)
    {
        var applied = await products.ApplyCatalogSnapshotAsync(request.SourceSnapshot, cancellationToken);
        return applied.Count;
    }
}
