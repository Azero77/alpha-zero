using System.Linq.Expressions;
using AlphaZero.Modules.Library.Application.Libraries.Queries.GetLibrary;
using AlphaZero.Modules.Library.Application.Queries;
using AlphaZero.Modules.Library.Application.RedemptionAuditLogs.GetRedemptionLogs;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Queries;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Library.Infrastructure.Queries;

public class LibraryQueryService : ILibraryQueryService
{
    private readonly AppDbContext _context;
    private readonly ITenantProvider _tenantProvider;

    public LibraryQueryService(AppDbContext context, ITenantProvider tenantProvider)
    {
        _context = context;
        _tenantProvider = tenantProvider;
    }

    public static readonly Expression<Func<AlphaZero.Modules.Library.Domain.Library, LibraryDto>> LibraryDtoProjection =
        l => new LibraryDto(
            l.Id,
            l.Name,
            l.Address,
            l.ContactNumber,
            l.AllowedResources.Select(r => r.Value).ToList());

    public static readonly Expression<Func<RedemptionAuditLog, RedemptionAuditLogDto>> RedemptionAuditLogDtoProjection =
        x => new RedemptionAuditLogDto(
            x.Id,
            x.AccessCodeId,
            x.LibraryId,
            x.RedeemedByUserId,
            x.StrategyId,
            x.TargetResourceArn.Value,
            x.RedeemedAt,
            x.IpAddress,
            x.DeviceFingerprint);

    public async Task<PagedResult<LibraryDto>> ListLibrariesAsync(int page, int perPage, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null || tenantId == Guid.Empty)
            return new PagedResult<LibraryDto>([], 0, page, perPage);

        var query = _context.Libraries
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(l => l.Name)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(LibraryDtoProjection)
            .ToListAsync(cancellationToken);

        return new PagedResult<LibraryDto>(items, totalCount, page, perPage);
    }

    public async Task<LibraryDto?> GetLibraryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null || tenantId == Guid.Empty)
            return null;

        return await _context.Libraries
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId.Value && l.Id == id)
            .Select(LibraryDtoProjection)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<RedemptionAuditLogDto>> GetRedemptionLogsAsync(
        Guid libraryId,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null || tenantId == Guid.Empty)
            return new PagedResult<RedemptionAuditLogDto>([], 0, page, pageSize);

        var query = _context.RedemptionAuditLogs
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.LibraryId == libraryId);

        if (from.HasValue)
        {
            var fromDate = DateTime.SpecifyKind(from.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(x => x.RedeemedAt >= fromDate);
        }

        if (to.HasValue)
        {
            var toDate = DateTime.SpecifyKind(to.Value.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);
            query = query.Where(x => x.RedeemedAt <= toDate);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.RedeemedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(RedemptionAuditLogDtoProjection)
            .ToListAsync(cancellationToken);

        return new PagedResult<RedemptionAuditLogDto>(items, totalCount, page, pageSize);
    }
}
