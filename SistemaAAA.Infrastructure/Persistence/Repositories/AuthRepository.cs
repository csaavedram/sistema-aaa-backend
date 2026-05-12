using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de operaciones de autenticación sobre EF Core.
/// </summary>
public class AuthRepository : IAuthRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="AuthRepository"/>.
    /// </summary>
    /// <param name="context">Contexto EF Core.</param>
    public AuthRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return _context.Users.FirstOrDefaultAsync(x => x.Email == email, ct);
    }

    /// <inheritdoc />
    public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
    {
        return _context.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
    }

    /// <inheritdoc />
    public Task<User?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        return GetByEmailAsync(email, ct);
    }

    /// <inheritdoc />
    public async Task<List<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(
                _context.Roles,
                ur => ur.RoleId,
                r => r.Id,
                (_, r) => r.Name)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public Task<RefreshToken?> GetRefreshTokenByTokenAsync(string token, CancellationToken ct = default)
    {
        var tokenHash = ComputeTokenHash(token);
        return _context.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);
    }

    /// <inheritdoc />
    public async Task SaveRefreshTokenAsync(Guid userId, string refreshToken, string ipAddress, CancellationToken ct = default)
    {
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = ComputeTokenHash(refreshToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        await _context.RefreshTokens.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RevokeRefreshTokenAsync(Guid tokenId, CancellationToken ct = default)
    {
        var token = await _context.RefreshTokens.FirstOrDefaultAsync(x => x.Id == tokenId, ct);
        if (token is null)
        {
            return;
        }

        token.IsRevoked = true;
        token.RevokedAt = DateTime.UtcNow;
        _context.RefreshTokens.Update(token);
        await _context.SaveChangesAsync(ct);
    }

    private static string ComputeTokenHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
