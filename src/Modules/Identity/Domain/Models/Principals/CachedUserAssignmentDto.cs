using System;
using System.Collections.Generic;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;

namespace AlphaZero.Modules.Identity.Domain.Models.Principals;

public class CachedUserAssignmentDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ResourceArn { get; set; } = string.Empty;
    public Guid PrincipalId { get; set; }
    public CachedTenantUserDto TenantUser { get; set; } = new();
    public CachedPrincipalDto Principal { get; set; } = new();
}

public class CachedTenantUserDto
{
    public Guid Id { get; set; }
    public string IdentityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? MainDeviceId { get; set; }
}

public class CachedPrincipalDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PrincipalType PrincipalType { get; set; }
    public string? PrincipalScopePattern { get; set; }
    public Guid TenantId { get; set; }
    public List<CachedPolicyDto> Policies { get; set; } = new();
}

public class CachedPolicyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PolicyType Type { get; set; }
    public List<CachedStatementDto> Statements { get; set; } = new();
}

public class CachedStatementDto
{
    public string Sid { get; set; } = string.Empty;
    public List<string> Actions { get; set; } = new();
    public bool Effect { get; set; }
    public List<string>? Resources { get; set; }
    public IConditionNode? Condition { get; set; }
}
