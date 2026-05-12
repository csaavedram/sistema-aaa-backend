namespace SistemaAAA.Domain;

/// <summary>
/// Representa un token de reinicio de contraseña de un solo uso.
/// </summary>
/// <remarks>
/// <para>
/// El token es de uso único: una vez consumido (IsUsed = true), no puede reutilizarse,
/// incluso si aún no ha expirado.
/// </para>
/// <para>
/// <strong>Seguridad:</strong> TokenHash almacena el hash SHA256 del token en claro,
/// NUNCA el token original. Esto evita exposición en caso de brecha de BD.
/// </para>
/// <para>
/// <strong>Validación:</strong> Un token es válido si:
/// <list type="bullet">
/// <item>IsUsed = false (aún no consumido)</item>
/// <item>ExpiresAt > DateTime.UtcNow (no ha expirado)</item>
/// </list>
/// </para>
/// </remarks>
public class PasswordResetToken
{
    /// <summary>
    /// Identificador único del token (GUID generado en aplicación).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Identificador del usuario propietario del token.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Hash SHA256 del token en claro.
    /// </summary>
    /// <remarks>
    /// CRÍTICO: Nunca almacenar el token original en claro.
    /// El repositorio genera el hash antes de persistir; las consultas buscan por hash.
    /// </remarks>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora de expiración del token (UTC).
    /// </summary>
    /// <remarks>
    /// Típicamente 1 hora desde la creación. Después de ExpiresAt, el token es inválido.
    /// </remarks>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Indica si el token ya ha sido consumido (token de uso único).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Valores:
    /// <list type="bullet">
    /// <item><c>false</c> — Token nuevo, aún no consumido</item>
    /// <item><c>true</c> — Token ya fue consumido; no puede reutilizarse incluso si no ha expirado</item>
    /// </list>
    /// </para>
    /// <para>
    /// Siempre se establece a <c>true</c> por MarkAsUsedAsync en el repositorio.
    /// </para>
    /// </remarks>
    public bool IsUsed { get; set; } = false;

    /// <summary>
    /// Fecha y hora de creación del token (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Fecha y hora de consumo del token (UTC), o null si aún no se ha consumido.
    /// </summary>
    /// <remarks>
    /// Se establece por MarkAsUsedAsync junto con IsUsed = true.
    /// </remarks>
    public DateTime? UsedAt { get; set; }
}
