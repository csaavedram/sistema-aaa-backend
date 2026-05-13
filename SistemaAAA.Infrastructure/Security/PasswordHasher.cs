using BCrypt.Net;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Infrastructure.Security;

/// <summary>
/// Implementación de `IPasswordHasher` usando BCrypt.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    public string Hash(string plain)
    {
        return BCrypt.Net.BCrypt.HashPassword(plain, workFactor: 12);
    }

    public bool Verify(string plain, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(plain, hash);
        }
        catch
        {
            return false;
        }
    }
}
