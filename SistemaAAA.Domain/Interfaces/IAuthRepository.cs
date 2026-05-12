using System.Threading;
using System.Threading.Tasks;
using SistemaAAA.Domain;

namespace SistemaAAA.Domain.Interfaces;

/// <summary>
/// Repositorio con operaciones de autenticación específicas.
/// </summary>
public interface IAuthRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un usuario por su identificador.
    /// </summary>
    Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Alias/compatibilidad: obtiene un usuario por su correo electrónico.
    /// </summary>
    Task<User?> GetUserByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los roles asociados a un usuario.
    /// </summary>
    Task<List<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Actualiza un usuario existente.
    /// </summary>
    /// <param name="user">Instancia del usuario a actualizar.</param>
    /// <param name="ct">Token de cancelación.</param>
    Task UpdateAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un refresh token por su valor en claro.
    /// </summary>
    Task<RefreshToken?> GetRefreshTokenByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Persiste un nuevo refresh token.
    /// </summary>
    Task SaveRefreshTokenAsync(Guid userId, string refreshToken, string ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Revoca un refresh token existente.
    /// </summary>
    Task RevokeRefreshTokenAsync(Guid tokenId, CancellationToken ct = default);
}
