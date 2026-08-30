namespace AlphaZero.Shared.Authorization;

public static class CustomClaimTypes
{
    public const string Sub = "sub";
    public const string Jti = "jti";
    public const string Iat = "iat";
    public const string TenantId = "tid";
    public const string AuthMethod = "auth_method";
    
    // TenantUser specific claims
    public const string IdentityId = "identity_id";
    public const string Name = "name";
    public const string DeviceId = "device_id";
    public const string DeviceName = "device_name";
    public const string DevicePlatform = "device_platform";
    public const string DevicePublicKey = "device_public_key";
    public const string MainDeviceId = "main_device_id";

    // Principal specific claims
    public const string Username = "username";
    public const string PrincipalType = "principal_type";
    public const string PrincipalScope = "principal_scope";
    public const string IsManaged = "is_managed";
    public const string IsGlobal = "is_global";
}
