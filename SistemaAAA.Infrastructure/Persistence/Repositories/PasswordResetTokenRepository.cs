using Microsoft.EntityFrameworkCore;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación del repositorio de tokens de reinicio de contraseña.
/// </summary>
public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="PasswordResetTokenRepository"/>.
    /// </summary>
    /// <param name="context">Contexto de base de datos.</param>
    public PasswordResetTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Persiste un nuevo token de reinicio de contraseña en la base de datos.
    /// </summary>
    /// <param name="token">Token de reinicio con propiedades ya configuradas (Id, UserId, TokenHash, etc.).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <exception cref="ArgumentNullException">Si token es nulo.</exception>
    public async Task CreateAsync(PasswordResetToken token, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(token, nameof(token));

        await _context.PasswordResetTokens.AddAsync(token, ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Busca un token de reinicio por su hash SHA256.
    /// </summary>
    /// <param name="tokenHash">Hash SHA256 del token (en Base64).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// Devuelve el token si existe; null en caso contrario.
    /// </returns>
    /// <remarks>
    /// La consulta se ejecuta sin tracking (AsNoTracking) para optimizar rendimiento.
    /// </remarks>
    public async Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(tokenHash, nameof(tokenHash));

        return await _context.PasswordResetTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);
    }

    /// <summary>
    /// Marca un token como consumido e inutilizable.
    /// </summary>
    /// <param name="tokenId">Id del token a marcar como usado.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <remarks>
    /// <para>
    /// Establece IsUsed = true y UsedAt = UtcNow.
    /// Esto garantiza que el token no pueda reutilizarse, incluso si aún no ha expirado.
    /// </para>
    /// <para>
    /// Este método debe invocarse SIEMPRE al consumir un token, incluso si la operación
    /// posterior (cambio de contraseña) falla, para garantizar single-use semantics.
    /// </para>
    /// </remarks>
    public async Task MarkAsUsedAsync(Guid tokenId, CancellationToken ct = default)
    {
        var token = await _context.PasswordResetTokens
            .FirstOrDefaultAsync(x => x.Id == tokenId, ct);

        if (token is null)
        {
            return;
        }

        token.IsUsed = true;
        token.UsedAt = DateTime.UtcNow;

        _context.PasswordResetTokens.Update(token);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Verifica si un usuario ya tiene un token de reinicio activo (válido y no consumido).
    /// </summary>
    /// <param name="userId">Id del usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// <c>true</c> si el usuario tiene al menos un token válido (IsUsed = false y ExpiresAt > UtcNow);
    /// <c>false</c> en caso contrario.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Útil para anti-spam: previene que el usuario reciba múltiples emails de reset
    /// si solicita la operación de forma repetida en corto tiempo.
    /// </para>
    /// <para>
    /// La consulta aprovecha el índice IX_PasswordResetTokens_UserId_Active para optimizar
    /// la búsqueda de tokens activos.
    /// </para>
    /// </remarks>
    public async Task<bool> HasActiveTokenAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.PasswordResetTokens
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && !x.IsUsed && x.ExpiresAt > DateTime.UtcNow, ct);
    }
}
