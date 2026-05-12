using System.Threading;
using System.Threading.Tasks;
using SistemaAAA.Domain;

namespace SistemaAAA.Domain.Interfaces;

/// <summary>
/// Repositorio para operaciones sobre roles y sus asignaciones.
/// </summary>
public interface IRoleRepository
{
    /// <summary>
    /// Obtiene un rol por su identificador.
    /// </summary>
    Task<Role?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Obtiene un rol por su nombre.
    /// </summary>
    Task<Role?> GetByNameAsync(string name, CancellationToken ct);

    /// <summary>
    /// Obtiene todos los roles del sistema.
    /// </summary>
    Task<IEnumerable<Role>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Crea un nuevo rol.
    /// </summary>
    Task CreateAsync(Role role, CancellationToken ct);

    /// <summary>
    /// Elimina un rol por su identificador.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Verifica si un rol está asignado a un usuario.
    /// </summary>
    Task<bool> IsAssignedToUserAsync(Guid roleId, Guid userId, CancellationToken ct);

    /// <summary>
    /// Asigna un rol a un usuario.
    /// </summary>
    Task AssignToUserAsync(Guid roleId, Guid userId, Guid assignedBy, CancellationToken ct);

    /// <summary>
    /// Quita un rol de un usuario.
    /// </summary>
    Task RemoveFromUserAsync(Guid roleId, Guid userId, CancellationToken ct);

    /// <summary>
    /// Obtiene todos los roles asignados a un usuario.
    /// </summary>
    Task<IEnumerable<Role>> GetRolesForUserAsync(Guid userId, CancellationToken ct);
}
