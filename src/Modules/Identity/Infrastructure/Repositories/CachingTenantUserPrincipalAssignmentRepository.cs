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

namespace AlphaZero.Modules.Identity.Infrastructure.Repositories;

public class CachingTenantUserPrincipalAssignmentRepository : CachingRepository<AppDbContext, TenantUserPrincipalAssignment, TenantUserPrincipalAssignmentRepository>
{
    public CachingTenantUserPrincipalAssignmentRepository(AppDbContext context, BaseRepository<AppDbContext, TenantUserPrincipalAssignment> innerRepository, HybridCache cache) : base(context, innerRepository, cache)
    {
    }
}
