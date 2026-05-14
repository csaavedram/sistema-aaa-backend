namespace SistemaAAA.Application.Features.Common;

/// <summary>
/// DTO para representar un permiso con sus propiedades esenciales.
/// Usado en respuestas de consultas de permisos.
/// </summary>
public class PermissionDto
{
    /// <summary>
    /// Identificador único del permiso.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Nombre del permiso.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Recurso protegido por este permiso.
    /// Por ejemplo: "User", "Role", "AuditLog".
    /// </summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>
    /// Acción permitida sobre el recurso.
    /// Por ejemplo: "Create", "Read", "Update", "Delete".
    /// </summary>
    public string Action { get; set; } = string.Empty;
}
