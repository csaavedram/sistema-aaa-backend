using System;
using System.Threading;
using System.Threading.Tasks;
using SistemaAAA.Domain;

namespace SistemaAAA.Domain.Interfaces;

/// <summary>
/// Repositorio para operaciones sobre Refresh Tokens.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Obtiene un token por su hash.
    /// </summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct);

    /// <summary>
    /// Crea un nuevo token de refresco.
    /// </summary>
    Task CreateAsync(RefreshToken token, CancellationToken ct);

    /// <summary>
    /// Revoca un token por su identificador.
    /// </summary>
    Task RevokeAsync(Guid tokenId, CancellationToken ct);

    /// <summary>
    /// Revoca todos los tokens asociados a un usuario.
    /// </summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct);
}
