namespace AlphaZero.Shared.Authorization;

public record AuthorizationContext
{
    public Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public string ResourcePath { get; init; } = string.Empty;
    public ResourceType ResourceType { get; init; }
    public string RequiredPermission { get; init; } = string.Empty;
    public required string AuthenticationMethod { get;init;  } 
    public string? DeviceId { get; init; }
    public string? UserMainDeviceId { get; init; }
    public string? Platform { get; init;  }
}

