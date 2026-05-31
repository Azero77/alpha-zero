using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Infrastructure.Models;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AlphaZero.Modules.Identity.Infrastructure.Repositories;

public class ManagedPolicyRepository : BaseRepository<AppDbContext, ManagedPolicy>, IManagedPolicyRepository
{
    public ManagedPolicyRepository(AppDbContext context) : base(context)
    {
    }

    public async Task AssignPolicyToPrincipal(Guid principalId, Guid managedPolicyId)
    {
        var assignment = new PrincipalPolicyAssignment
        {
            PrincipalId = principalId,
            ManagedPolicyId = managedPolicyId
        };

        await _context.PrincipalPolicyAssignments.AddAsync(assignment);
    }

    public async Task RemovePolicyFromPrincipal(Guid principalId, Guid managedPolicyId)
    {
        var assignment = await _context.PrincipalPolicyAssignments
            .FirstOrDefaultAsync(a => a.PrincipalId == principalId && a.ManagedPolicyId == managedPolicyId);

        if (assignment != null)
        {
            _context.PrincipalPolicyAssignments.Remove(assignment);
        }
    }
}

public class PrincipalRepository : IPrincipalRepository
{
    private readonly AppDbContext _context;

    public PrincipalRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Principal?> GetById(Guid id, CancellationToken token = default)
    {
        var dataModel = await _context.Principals
            .Include(p => p.ManagedPolicies)
            .FirstOrDefaultAsync(p => p.Id == id, token);

        return MapToDomain(dataModel);
    }

    public async Task<Principal?> GetFirst(Expression<Func<Principal, bool>> predicate, CancellationToken token = default)
    {
        var dataModels = await _context.Principals
            .Include(p => p.ManagedPolicies)
            .ToListAsync(token);
        
        return dataModels.Select(MapToDomain).AsQueryable().FirstOrDefault(predicate);
    }

    public void Add(Principal entity)
    {
        var dataModel = MapToDataModel(entity);
        _context.Principals.Add(dataModel);
    }

    public void Update(Principal entity)
    {
        var existing = _context.Principals
            .Include(p => p.ManagedPolicies)
            .FirstOrDefault(p => p.Id == entity.Id);

        if (existing != null)
        {
            existing.Username = entity.Username;
            existing.PasswordHash = entity.PasswordHash;
            existing.Name = entity.Name;
            existing.PrincipalType = entity.PrincipalType;
            existing.PrincipalScopePattern = entity.PrincipalScope?.Value;
            existing.TenantId = entity.TenantId;
            existing.InlinePolicies = entity.Policies.OfType<InlinePolicy>().ToList();
            
            existing.ManagedPolicies.Clear();
            existing.ManagedPolicies.AddRange(entity.Policies.OfType<ManagedPolicy>());
        }
    }

    public void Remove(Principal entity)
    {
        var existing = _context.Principals.Find(entity.Id);
        if (existing != null) _context.Principals.Remove(existing);
    }

    public async Task<bool> Any(Expression<Func<Principal, bool>> predicate, CancellationToken token = default)
    {
        var dataModels = await _context.Principals.ToListAsync(token);
        return dataModels.Select(MapToDomain).AsQueryable().Any(predicate);
    }

    public async Task<IReadOnlyCollection<Principal>> GetPrincipalsByResourceAsync(Guid resourceId, ResourceType resourceType, CancellationToken ct = default)
    {
        return new List<Principal>();
    }

    private Principal? MapToDomain(PrincipalDataModel? dataModel)
    {
        if (dataModel == null) return null;

        var principalResult = Principal.Create(
            dataModel.Id,
            dataModel.Username,
            dataModel.PasswordHash,
            dataModel.Name,
            dataModel.PrincipalType,
            dataModel.PrincipalScopePattern,
            dataModel.TenantId);

        if (principalResult.IsError) return null;

        var principal = principalResult.Value;
        
        var allPolicies = dataModel.InlinePolicies.Cast<IPolicy>()
            .Concat(dataModel.ManagedPolicies.Cast<IPolicy>());
        
        principal.LoadPolicies(allPolicies);

        return principal;
    }

    private PrincipalDataModel MapToDataModel(Principal entity)
    {
        return new PrincipalDataModel
        {
            Id = entity.Id,
            Username = entity.Username,
            PasswordHash = entity.PasswordHash,
            Name = entity.Name,
            PrincipalType = entity.PrincipalType,
            PrincipalScopePattern = entity.PrincipalScope?.Value,
            TenantId = entity.TenantId,
            InlinePolicies = entity.Policies.OfType<InlinePolicy>().ToList(),
            ManagedPolicies = entity.Policies.OfType<ManagedPolicy>().ToList()
        };
    }

    public Task<Principal?> GetById(Guid id) => GetById(id, default);
}

public class TenantUserRepository : BaseRepository<AppDbContext, TenantUser>
{
    public TenantUserRepository(AppDbContext context) : base(context)
    {
    }
}
