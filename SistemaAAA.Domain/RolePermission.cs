namespace SistemaAAA.Domain;

/// <summary>
/// Representa la asignación de un permiso a un rol.
/// Entidad de relación muchos-a-muchos entre Role y Permission.
/// </summary>
public class RolePermission
{
    /// <summary>
    /// Identificador del rol.
    /// Clave foránea a Role.Id.
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Identificador del permiso.
    /// Clave foránea a Permission.Id.
    /// </summary>
    public Guid PermissionId { get; set; }
}
