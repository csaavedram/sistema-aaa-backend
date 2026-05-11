namespace SistemaAAA.Domain;

/// <summary>
/// Representa un token de refresco (refresh token).
/// Se utiliza para obtener nuevos tokens de acceso sin volver a iniciar sesión.
/// </summary>
public class RefreshToken
{
    /// <summary>
    /// Identificador único del token de refresco.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Identificador del usuario propietario del token.
    /// Clave foránea a User.Id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Hash del token de refresco.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora de expiración del token de refresco.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Indica si el token ha sido revocado.
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    /// <summary>
    /// Fecha y hora de creación del token de refresco.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Fecha y hora en que fue revocado el token.
    /// Nulo si el token no ha sido revocado.
    /// </summary>
    public DateTime? RevokedAt { get; set; }
}
