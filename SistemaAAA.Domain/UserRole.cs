namespace SistemaAAA.Domain;

/// <summary>
/// Representa la asignación de un rol a un usuario.
/// Entidad de relación muchos-a-muchos entre User y Role.
/// </summary>
public class UserRole
{
    /// <summary>
    /// Identificador del usuario.
    /// Clave foránea a User.Id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Identificador del rol.
    /// Clave foránea a Role.Id.
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Fecha y hora en que se asignó el rol al usuario.
    /// </summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>
    /// Identificador del usuario que realizó la asignación.
    /// Clave foránea a User.Id.
    /// </summary>
    public Guid AssignedBy { get; set; }
}
