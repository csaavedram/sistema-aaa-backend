namespace SistemaAAA.Domain.Interfaces;

/// <summary>
/// Repositorio para gestionar tokens de reinicio de contraseña.
/// </summary>
/// <remarks>
/// Responsabilidades:
/// <list type="bullet">
/// <item>Persistencia de tokens hasheados (SHA256)</item>
/// <item>Consultas por hash para validación</item>
/// <item>Marcado de tokens como consumidos (single-use)</item>
/// <item>Prevención de spam: verificar si el usuario ya tiene un token activo</item>
/// </list>
/// </remarks>
public interface IPasswordResetTokenRepository
{
    /// <summary>
    /// Persiste un nuevo token de reinicio de contraseña.
    /// </summary>
    /// <param name="token">
    /// Token ya configurado con Id (GUID), UserId, TokenHash (SHA256), ExpiresAt, CreatedAt.
    /// IsUsed debe ser false por defecto.
    /// </param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea completada cuando el token se persista en BD.</returns>
    /// <remarks>
    /// <para>
    /// El token debe llegaryahasheado. El repositorio NO genera hashes; eso corresponde
    /// a la capa Application (ForgotPasswordCommandHandler o similar).
    /// </para>
    /// <para>
    /// Se recomienda validar que el token sea nuevo (Id != Guid.Empty, CreatedAt poblada, etc.)
    /// antes de invocar este método, aunque la responsabilidad principal es del handler.
    /// </para>
    /// </remarks>
    Task CreateAsync(PasswordResetToken token, CancellationToken ct = default);

    /// <summary>
    /// Busca un token de reinicio por su hash SHA256.
    /// </summary>
    /// <param name="tokenHash">Hash SHA256 del token (base64 o hex, según implementación).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// Devuelve el token si existe; null en caso contrario.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Este método es el punto de validación de tokens: la capa Application lo usa
    /// en ResetPasswordCommandHandler para verificar que el token existe y recuperar
    /// sus propiedades (UserId, ExpiresAt, IsUsed).
    /// </para>
    /// <para>
    /// No valida expiración ni estado (IsUsed); eso corresponde al handler.
    /// </para>
    /// </remarks>
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// Marca un token como consumido e inutilizable.
    /// </summary>
    /// <param name="tokenId">Id del token a marcar como usado.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea completada cuando IsUsed = true y UsedAt = UtcNow se persistan en BD.</returns>
    /// <remarks>
    /// <para>
    /// <strong>CRÍTICO:</strong> Invocar SIEMPRE al consumir un token, incluso si
    /// la operación posterior (cambio de contraseña, etc.) falla. Esto garantiza
    /// que el token sea de un solo uso.
    /// </para>
    /// <para>
    /// Transaccionalidad: se recomienda que el handler que consume el token
    /// combine esta llamada con la actualización de User.PasswordHash en una
    /// transacción explícita si es posible, para garantizar atomicidad.
    /// </para>
    /// </remarks>
    Task MarkAsUsedAsync(Guid tokenId, CancellationToken ct = default);

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
    /// Útil para anti-spam: ForgotPasswordCommandHandler debe verificar esta condición
    /// antes de generar un nuevo token. Si el usuario ya tiene uno activo, denegar
    /// la solicitud (respuesta idéntica para anti-enumeration).
    /// </para>
    /// <para>
    /// Ejemplo de bloqueo:
    /// <code>
    /// if (await _passwordResetTokenRepository.HasActiveTokenAsync(userId, ct))
    /// {
    ///     return Result&lt;Unit&gt;.Success(Unit.Value); // Response idéntica (anti-enumeration)
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    Task<bool> HasActiveTokenAsync(Guid userId, CancellationToken ct = default);
}
