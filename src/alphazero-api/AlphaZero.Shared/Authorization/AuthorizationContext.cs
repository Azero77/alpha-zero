using AlphaZero.Shared.Domain;

namespace AlphaZero.Shared.Authorization;

public record AuthorizationContext
{
    public static readonly AuthorizationContext Empty = new()
    {
        AuthenticationMethod = string.Empty
    };

    public Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public string ResourcePath { get; init; } = string.Empty;
    public string ResourceType { get; init; } = string.Empty;
    public string? RequiredPermission { get; init; } = null;
    public required string AuthenticationMethod { get; init; } 
    public string? DeviceId { get; init; }
    public string? UserMainDeviceId { get; init; }
    public string? IpAddress { get; init; }

    public AuthorizationContext ForResource(string requiredPermission, ResourceArn arn) => this with
    {
        RequiredPermission = requiredPermission,
        ResourcePath = arn.ResourcePath,
        ResourceType = arn.Service,
        TenantId = arn.TenantIdString.Equals(ResourceArn.GlobalTenant, StringComparison.OrdinalIgnoreCase)
            ? (TenantId ?? Guid.Empty)
            : Guid.Parse(arn.TenantIdString)
    };
}

public record TenantUserAuthorizationContext : AuthorizationContext
{
    public required string IdentityId { get; init; }
    public required string Name { get; init; }
    public string? PublicKey { get; init; }
    public string? DeviceName { get; init; }
    public string? MainDeviceId { get; init; }
    public string? DevicePlatform { get; init; }
}

public record PrincipalUserAuthorizationContext : AuthorizationContext
{
    public Guid PrincipalId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string PrincipalType { get; init; } = string.Empty;
    public bool IsManaged { get; init; }
    public bool IsGlobal { get; init; }
    public string? PrincipalScope { get; init; }
}
