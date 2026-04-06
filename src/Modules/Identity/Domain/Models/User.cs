using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Identity.Domain.Models;

/// <summary>
/// Global User entity for Authentication.
/// Not scoped by TenantId because a user can belong to multiple tenants.
/// </summary>
public class User : AggregateRoot
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    
    /// <summary>
    /// A unique stamp that changes whenever the user's credentials change
    /// or when all sessions need to be invalidated (the "Kill Switch").
    /// </summary>
    public string SecurityStamp { get; private set; } = Guid.NewGuid().ToString();

    // EF Core Constructor
    protected User() { }

    public User(Guid id, string email, string passwordHash, string fullName) : base(id)
    {
        Email = email.ToLowerInvariant();
        PasswordHash = passwordHash;
        FullName = fullName;
    }

    public void UpdatePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        InvalidateSessions();
    }

    public void InvalidateSessions()
    {
        SecurityStamp = Guid.NewGuid().ToString();
    }
}
