namespace SistemaAAA.Domain;

/// <summary>
/// Representa un rol en el sistema.
/// Los roles agrupan permisos y se asignan a usuarios.
/// </summary>
public class Role
{
    /// <summary>
    /// Identificador único del rol.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Nombre del rol.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descripción del rol y sus responsabilidades.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Indica si es un rol de sistema.
    /// Los roles de sistema no pueden ser eliminados.
    /// </summary>
    public bool IsSystem { get; set; }
}
