namespace SistemaAAA.Domain;

/// <summary>
/// Representa un permiso en el sistema.
/// Los permisos se asignan a roles.
/// </summary>
public class Permission
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
