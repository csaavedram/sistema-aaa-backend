namespace SistemaAAA.Domain;

/// <summary>
/// Representa un registro de auditoría del sistema.
/// Es una entidad inmutable que registra todos los eventos de auditoría.
/// NO contiene campos UpdatedAt ni IsDeleted por su naturaleza inmutable.
/// </summary>
public class AuditLog
{
    /// <summary>
    /// Identificador único del registro de auditoría.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Identificador del usuario que realizó la acción.
    /// Nulo para intentos de acceso anónimos.
    /// Clave foránea a User.Id.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Tipo de evento registrado.
    /// Por ejemplo: "LoginAttempt", "PermissionDenied", "UserCreated", etc.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Recurso afectado por la acción.
    /// Por ejemplo: "User", "Role", "Permission", etc.
    /// </summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>
    /// Detalles adicionales del evento en formato JSON.
    /// Contiene información adicional relevante para el evento.
    /// </summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// Dirección IP desde la que se realizó la acción.
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora de creación del registro de auditoría.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
