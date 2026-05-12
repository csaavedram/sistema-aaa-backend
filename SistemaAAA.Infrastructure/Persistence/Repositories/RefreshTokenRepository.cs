using Microsoft.EntityFrameworkCore;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación del repositorio de refresh tokens.
/// </summary>
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="RefreshTokenRepository"/>.
    /// </summary>
    /// <param name="context">Contexto EF Core.</param>
    public RefreshTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct)
    {
        return _context.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);
    }

    /// <inheritdoc />
    public async Task CreateAsync(RefreshToken token, CancellationToken ct)
    {
        await _context.RefreshTokens.AddAsync(token, ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RevokeAsync(Guid tokenId, CancellationToken ct)
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

    /// <inheritdoc />
    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(x => x.UserId == userId && !x.IsRevoked)
            .ToListAsync(ct);

        if (activeTokens.Count == 0)
        {
            return;
        }

        var revokedAt = DateTime.UtcNow;
        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = revokedAt;
        }

        _context.RefreshTokens.UpdateRange(activeTokens);
        await _context.SaveChangesAsync(ct);
    }
}
