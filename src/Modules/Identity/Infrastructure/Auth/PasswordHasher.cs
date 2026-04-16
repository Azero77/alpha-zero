using AlphaZero.Shared.Domain;
using System.Security.Cryptography;
using System.Text;

namespace AlphaZero.Modules.Identity.Infrastructure.Auth;

public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        // Simple implementation for now. In production, use BCrypt or Argon2.
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }
}
