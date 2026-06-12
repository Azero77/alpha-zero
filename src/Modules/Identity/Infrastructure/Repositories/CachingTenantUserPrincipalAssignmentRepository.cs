using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Domain;
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

public class CachingTenantUserPrincipalAssignmentRepository : ITenantUserPrincipalAssignmentRepository
{
    private readonly ITenantUserPrincipalAssignmentRepository _inner;
    private readonly HybridCache _cache;

    public CachingTenantUserPrincipalAssignmentRepository(
        ITenantUserPrincipalAssignmentRepository inner,
        HybridCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public IQueryable<TenantUserPrincipalAssignment> Entities => _inner.Entities;

    public void Add(TenantUserPrincipalAssignment entity)
    {
        _inner.Add(entity);
        EvictCache(entity);
    }

    public void Remove(TenantUserPrincipalAssignment entity)
    {
        _inner.Remove(entity);
        EvictCache(entity);
    }

    public void Update(TenantUserPrincipalAssignment entity)
    {
        _inner.Update(entity);
        EvictCache(entity);
    }

    private void EvictCache(TenantUserPrincipalAssignment entity)
    {
        if (entity.TenantUser != null)
        {
            _ = _cache.RemoveAsync($"auth_assignments:{entity.TenantUser.Id}", default);
        }
    }

    public async Task<List<TenantUserPrincipalAssignment>> GetActiveAssignments(Guid tenantUserId, string? resourceArn = null)
    {
        var cacheKey = $"auth_assignments:{tenantUserId}";

        var cachedDtoList = await _cache.GetOrCreateAsync<List<CachedUserAssignmentDto>>(
            cacheKey,
            async token =>
            {
                // Fetch all assignments for the user without resource filtering
                var assignments = await _inner.GetActiveAssignments(tenantUserId, null);
                return assignments.Select(MapToDto).ToList();
            },
            cancellationToken: default
        );

        if (cachedDtoList == null || !cachedDtoList.Any())
            return new List<TenantUserPrincipalAssignment>();

        // Reconstruct assignments
        var allAssignments = cachedDtoList.Select(ReconstructAssignment).ToList();

        // Perform resource filtering in-memory
        if (string.IsNullOrEmpty(resourceArn) || resourceArn == "*")
        {
            return allAssignments;
        }

        var arnResult = ResourceArn.Create(resourceArn);
        if (arnResult.IsError) return new List<TenantUserPrincipalAssignment>();
        var targetArn = arnResult.Value;

        return allAssignments.Where(a =>
            a.Resource.TenantIdString.Equals(targetArn.TenantIdString, StringComparison.OrdinalIgnoreCase) &&
            (targetArn.ResourcePath.Equals(a.Resource.ResourcePath, StringComparison.OrdinalIgnoreCase) ||
             targetArn.ResourcePath.StartsWith(a.Resource.ResourcePath + "/", StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public Task<IReadOnlyCollection<TenantUserPrincipalAssignment>> GetAll(CancellationToken token = default) => _inner.GetAll(token);

    public Task<IReadOnlyCollection<TenantUserPrincipalAssignment>> Get(Expression<Func<TenantUserPrincipalAssignment, bool>> filter, CancellationToken token = default) => _inner.Get(filter, token);

    public Task<TenantUserPrincipalAssignment?> GetFirst(Expression<Func<TenantUserPrincipalAssignment, bool>> filter, CancellationToken token = default) => _inner.GetFirst(filter, token);

    public Task<bool> Any(Expression<Func<TenantUserPrincipalAssignment, bool>> filter, CancellationToken token = default) => _inner.Any(filter, token);

    public Task<int> Count(Expression<Func<TenantUserPrincipalAssignment, bool>>? filter = null, CancellationToken token = default) => _inner.Count(filter, token);

    public Task<PagedResult<TenantUserPrincipalAssignment>> Get<TKey>(int pageNumber, int perPage, Expression<Func<TenantUserPrincipalAssignment, TKey>> orderBy, bool ascending = true, CancellationToken token = default)
        => _inner.Get(pageNumber, perPage, orderBy, ascending, token);

    public Task<PagedResult<TenantUserPrincipalAssignment>> Get<TKey>(int pageNumber, int perPage, Expression<Func<TenantUserPrincipalAssignment, bool>> filter, Expression<Func<TenantUserPrincipalAssignment, TKey>> orderBy, bool ascending = true, CancellationToken token = default)
        => _inner.Get(pageNumber, perPage, filter, orderBy, ascending, token);

    public Task<TenantUserPrincipalAssignment?> GetById(Guid id, CancellationToken token = default) => _inner.GetById(id, token);

    // --- MAPPING HELPERS ---

    private static CachedUserAssignmentDto MapToDto(TenantUserPrincipalAssignment assignment)
    {
        var dto = new CachedUserAssignmentDto
        {
            Id = assignment.Id,
            TenantId = assignment.TenantId,
            ResourceArn = assignment.Resource.Value,
            PrincipalId = assignment.PrincipalId,
        };

        if (assignment.TenantUser != null)
        {
            dto.TenantUser = new CachedTenantUserDto
            {
                Id = assignment.TenantUser.Id,
                IdentityId = assignment.TenantUser.IdentityId,
                Name = assignment.TenantUser.Name,
                MainDeviceId = assignment.TenantUser.MainDeviceId
            };
        }

        if (assignment.Principal != null)
        {
            dto.Principal = new CachedPrincipalDto
            {
                Id = assignment.Principal.Id,
                Username = assignment.Principal.Username,
                PasswordHash = assignment.Principal.PasswordHash,
                Name = assignment.Principal.Name,
                PrincipalType = assignment.Principal.PrincipalType,
                PrincipalScopePattern = assignment.Principal.PrincipalScope?.Value,
                TenantId = assignment.Principal.TenantId,
                Policies = assignment.Policies.Select(policy =>
                {
                    var policyDto = new CachedPolicyDto
                    {
                        Id = policy.Id,
                        Name = policy.Name,
                        Type = policy.Type
                    };

                    if (policy is ManagedPolicy managedPolicy)
                    {
                        policyDto.Statements = managedPolicy.Statements.Select(s => new CachedStatementDto
                        {
                            Sid = s.Sid,
                            Actions = s.Actions,
                            Effect = s.Effect,
                            Condition = s.Condition,
                            Resources = null
                        }).ToList();
                    }
                    else if (policy is InlinePolicy inlinePolicy)
                    {
                        policyDto.Statements = inlinePolicy.Statements.Select(s => new CachedStatementDto
                        {
                            Sid = s.Sid,
                            Actions = s.Actions,
                            Effect = s.Effect,
                            Condition = s.Condition,
                            Resources = s.Resources.Select(r => r.Value).ToList()
                        }).ToList();
                    }

                    return policyDto;
                }).ToList()
            };
        }

        return dto;
    }

    private static TenantUserPrincipalAssignment ReconstructAssignment(CachedUserAssignmentDto dto)
    {
        // 1. Reconstruct TenantUser
        var tenantUser = (TenantUser)Activator.CreateInstance(
            typeof(TenantUser),
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new object[] { dto.TenantUser.Id, dto.TenantId, dto.TenantUser.IdentityId, dto.TenantUser.Name },
            null)!;

        if (dto.TenantUser.MainDeviceId.HasValue)
        {
            typeof(TenantUser).GetProperty("MainDeviceId")?.SetValue(tenantUser, dto.TenantUser.MainDeviceId.Value);
        }

        // 2. Reconstruct Principal
        var principalResult = Principal.Create(
            dto.Principal.Id,
            dto.Principal.Username,
            dto.Principal.PasswordHash,
            dto.Principal.Name,
            dto.Principal.PrincipalType,
            dto.Principal.PrincipalScopePattern,
            dto.Principal.TenantId);

        var principal = principalResult.Value;

        // Load policies
        foreach (var policyDto in dto.Principal.Policies)
        {
            var policy = ReconstructPolicy(policyDto, dto.Principal.TenantId);
            principal.AddPolicy(policy);
        }

        // 3. Reconstruct ResourceArn
        var arnResult = ResourceArn.Create(dto.ResourceArn);
        var arn = arnResult.Value;

        // 4. Reconstruct Assignment
        var assignment = (TenantUserPrincipalAssignment)Activator.CreateInstance(
            typeof(TenantUserPrincipalAssignment),
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new object[] { dto.Id, dto.TenantId, tenantUser, principal, arn },
            null)!;

        return assignment;
    }

    private static IPolicy ReconstructPolicy(CachedPolicyDto policyDto, Guid tenantId)
    {
        if (policyDto.Type == PolicyType.Managed)
        {
            var statements = policyDto.Statements.Select(s => new ManagedPolicyStatement(
                s.Sid,
                s.Actions,
                s.Effect,
                s.Condition
            )).ToList();
            return new ManagedPolicy(policyDto.Id, policyDto.Name, statements);
        }
        else
        {
            var inlinePolicy = new InlinePolicy(policyDto.Id, policyDto.Name, tenantId);
            foreach (var s in policyDto.Statements)
            {
                var resourcesList = new List<ResourcePattern>();
                if (s.Resources != null)
                {
                    foreach (var resStr in s.Resources)
                    {
                        var resPatternResult = ResourcePattern.Create(resStr);
                        if (!resPatternResult.IsError)
                        {
                            resourcesList.Add(resPatternResult.Value);
                        }
                    }
                }
                var statement = new PolicyStatement(
                    s.Sid,
                    s.Actions,
                    s.Effect,
                    resourcesList,
                    s.Condition
                );
                inlinePolicy.AddStatement(statement);
            }
            return inlinePolicy;
        }
    }
}
