using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Shared.Domain;
using System.Text.Json;

namespace AlphaZero.Modules.Identity.Infrastructure.Serialization;

/// <summary>
/// JSON-safe projection of an assignment graph. HybridCache serialises even its L1 entries,
/// so domain aggregates with private constructors and read-only collections cannot be cached directly.
/// </summary>
internal sealed record CachedAssignment(
    Guid Id,
    Guid TenantId,
    string ResourceArn,
    DateTime TimeCreated,
    CachedTenantUser TenantUser,
    CachedPrincipal Principal);

internal sealed record CachedTenantUser(
    Guid Id,
    Guid TenantId,
    string IdentityId,
    string Name,
    Guid? MainDeviceId,
    DateTime? LastMainDeviceSwitchDate);

internal sealed record CachedPrincipal(
    Guid Id,
    string Username,
    string PasswordHash,
    string Name,
    PrincipalType PrincipalType,
    string? PrincipalScope,
    Guid TenantId,
    List<CachedManagedPolicy> ManagedPolicies,
    List<CachedInlinePolicy> InlinePolicies);

internal sealed record CachedManagedPolicy(Guid Id, string Name, List<CachedManagedStatement> Statements);

internal sealed record CachedManagedStatement(string Sid, List<string> Actions, bool Effect, string? ConditionJson);

internal sealed record CachedInlinePolicy(Guid Id, string Name, Guid TenantId, List<CachedPolicyStatement> Statements);

internal sealed record CachedPolicyStatement(string Sid, List<string> Actions, bool Effect, List<string> ResourcePatterns, string? ConditionJson);

internal static class IdentityCacheJson
{
    /// <summary>
    /// PascalCase to match the EF converters: <see cref="ConditionNodeJsonConverter"/> looks for a "Type" discriminator.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new ConditionNodeJsonConverter() }
    };
}

internal static class CachedAssignmentMapper
{
    public static List<CachedAssignment> ToCache(IReadOnlyCollection<TenantUserPrincipalAssignment> assignments)
        => assignments.Select(ToCache).ToList();

    public static List<TenantUserPrincipalAssignment> ToDomain(IReadOnlyCollection<CachedAssignment> assignments)
        => assignments.Select(ToDomain).ToList();

    private static CachedAssignment ToCache(TenantUserPrincipalAssignment assignment) => new(
        assignment.Id,
        assignment.TenantId,
        assignment.Resource.Value,
        assignment.TimeCreated,
        new CachedTenantUser(
            assignment.TenantUser.Id,
            assignment.TenantUser.TenantId,
            assignment.TenantUser.IdentityId,
            assignment.TenantUser.Name,
            assignment.TenantUser.MainDeviceId,
            assignment.TenantUser.LastMainDeviceSwitchDate),
        new CachedPrincipal(
            assignment.Principal.Id,
            assignment.Principal.Username,
            assignment.Principal.PasswordHash,
            assignment.Principal.Name,
            assignment.Principal.PrincipalType,
            assignment.Principal.PrincipalScope?.Value,
            assignment.Principal.TenantId,
            assignment.Principal.Policies.OfType<ManagedPolicy>()
                .Select(p => new CachedManagedPolicy(
                    p.Id,
                    p.Name,
                    p.Statements.Select(s => new CachedManagedStatement(s.Sid, s.Actions, s.Effect, SerializeCondition(s.Condition))).ToList()))
                .ToList(),
            assignment.Principal.Policies.OfType<InlinePolicy>()
                .Select(p => new CachedInlinePolicy(
                    p.Id,
                    p.Name,
                    p.TenantId,
                    p.Statements.Select(s => new CachedPolicyStatement(
                        s.Sid,
                        s.Actions,
                        s.Effect,
                        s.Resources.Select(r => r.Value).ToList(),
                        SerializeCondition(s.Condition))).ToList()))
                .ToList()));

    private static TenantUserPrincipalAssignment ToDomain(CachedAssignment cached)
    {
        var tenantUser = TenantUser.Hydrate(
            cached.TenantUser.Id,
            cached.TenantUser.TenantId,
            cached.TenantUser.IdentityId,
            cached.TenantUser.Name,
            cached.TenantUser.MainDeviceId,
            cached.TenantUser.LastMainDeviceSwitchDate);

        var principal = Principal.Create(
            cached.Principal.Id,
            cached.Principal.Username,
            cached.Principal.PasswordHash,
            cached.Principal.Name,
            cached.Principal.PrincipalType,
            cached.Principal.PrincipalScope,
            cached.Principal.TenantId).Value;

        foreach (var managed in cached.Principal.ManagedPolicies)
        {
            var statements = managed.Statements
                .Select(s => new ManagedPolicyStatement(s.Sid, s.Actions, s.Effect, DeserializeCondition(s.ConditionJson)))
                .ToList();
            principal.AddPolicy(new ManagedPolicy(managed.Id, managed.Name, statements));
        }

        foreach (var inline in cached.Principal.InlinePolicies)
        {
            var policy = new InlinePolicy(inline.Id, inline.Name, inline.TenantId);
            foreach (var statement in inline.Statements)
            {
                policy.AddStatement(new PolicyStatement(
                    statement.Sid,
                    statement.Actions,
                    statement.Effect,
                    statement.ResourcePatterns.Select(r => ResourcePattern.Create(r).Value).ToList(),
                    DeserializeCondition(statement.ConditionJson)));
            }
            principal.AddPolicy(policy);
        }

        return TenantUserPrincipalAssignment.Hydrate(
            cached.Id,
            cached.TenantId,
            tenantUser,
            principal,
            ResourceArn.Create(cached.ResourceArn).Value,
            cached.TimeCreated);
    }

    private static string? SerializeCondition(IConditionNode? condition)
        => condition is null ? null : JsonSerializer.Serialize(condition, IdentityCacheJson.Options);

    private static IConditionNode? DeserializeCondition(string? json)
        => json is null ? null : JsonSerializer.Deserialize<IConditionNode>(json, IdentityCacheJson.Options);
}
