using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Queries;
using Microsoft.Extensions.Caching.Hybrid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static Amazon.S3.Util.S3EventNotification;

namespace AlphaZero.Modules.Identity.Infrastructure.Repositories;

public class CachingTenantUserPrincipalAssignmentRepository : CachingRepository<AppDbContext, TenantUserPrincipalAssignment, TenantUserPrincipalAssignmentRepository>, ITenantUserPrincipalAssignmentRepository
{
    public CachingTenantUserPrincipalAssignmentRepository(AppDbContext context, TenantUserPrincipalAssignmentRepository innerRepository, HybridCache cache) : base(context, innerRepository, cache)
    {
    }

    public async Task<TenantUserPrincipalAssignment?> GetActiveAssignment(Guid tenantUserId, string? resourceArn = null, CancellationToken ct = default)
    {
        string key = $"tenant_user_assignments:{tenantUserId}:{resourceArn}";
        return await _cache.GetOrCreateAsync(
            key: key,
            async token => await _innerRepository.GetActiveAssignment(tenantUserId, resourceArn),
            cancellationToken: ct
            );
    }
}
