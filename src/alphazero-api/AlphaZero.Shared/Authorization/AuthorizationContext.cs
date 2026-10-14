namespace AlphaZero.Shared.Authorization;

public record AuthorizationContext
{
    public Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public string ResourcePath { get; init; } = string.Empty;
    public string ResourceType { get; init; } = string.Empty;
    public string? RequiredPermission { get; init; } = null;
    public required string AuthenticationMethod { get; init; } 
    public string? DeviceId { get; init; }
    //public string? UserMainDeviceId { get; init; } , The AuthorizationContext is just data about the user that does not need querying
    public string? IpAddress { get; init; }
}



public record TenantUserAuthorizationContext : AuthorizationContext
{
    public required string IdentityId { get; init; }
    public required string Name { get; init; }
    private string? PublicKey { get; init; }
    public string DeviceId { get; init; }
    public string DeviceName { get; init; }
    public string MainDeviceId { get; init; }
    public string DevicePlatform { get; init; }
    
}

public record PrincipalUserAuthorizationContext() : AuthorizationContext
{
    public Guid PrincipalId { get; init; }
    public string Username { get; init; }
    public string Name { get; init; }
    public string PrincipalType { get; init; }
    public bool IsManaged { get; init; }
    public bool IsGlobal { get; init; }
    public string? PrincipalScope { get; init; }
}
public record GlobalUserAuthorizationContext() : AuthorizationContext();
