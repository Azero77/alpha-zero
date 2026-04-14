using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Identity.Infrastructure.Repositories;

public class TenantUserPrincpialAssignmentRepository : BaseRepository<AppDbContext, TenantPrinciaplAssignment>, ITenantUserPrincpialAssignmentRepository
{
    public TenantUserPrincpialAssignmentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<TenantPrinciaplAssignment?> Get(Guid tenantUserId, string resourceArn)
    {
        return await _context.TenantPrinciaplAssignments
            .Include(a => a.Principal)
                .ThenInclude(p => p.ManagedPolicies)
            .Include(a => a.TenantUser)
            .FirstOrDefaultAsync(a => a.TenantUser.Id == tenantUserId && a.Resource.ToString() == resourceArn);
    }
}
