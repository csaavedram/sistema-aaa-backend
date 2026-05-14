using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SistemaAAA.Domain;

namespace SistemaAAA.Domain.Interfaces;

/// <summary>
/// Repositorio para operaciones sobre permisos y su asignación a roles.
/// </summary>
public interface IPermissionRepository
{
    /// <summary>
    /// Obtiene un permiso por su identificador.
    /// </summary>
    Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Obtiene todos los permisos del sistema.
    /// </summary>
    Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Obtiene todos los permisos asignados a un rol.
    /// </summary>
    Task<IEnumerable<Permission>> GetByRoleIdAsync(Guid roleId, CancellationToken ct);

    /// <summary>
    /// Obtiene todos los permisos de un usuario sumando los de todos sus roles.
    /// </summary>
    Task<IEnumerable<Permission>> GetByUserIdAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Asigna un permiso a un rol.
    /// </summary>
    Task AssignToRoleAsync(Guid roleId, Guid permissionId, CancellationToken ct);

    /// <summary>
    /// Quita un permiso de un rol.
    /// </summary>
    Task RemoveFromRoleAsync(Guid roleId, Guid permissionId, CancellationToken ct);

    /// <summary>
    /// Verifica si un permiso está asignado a un rol.
    /// </summary>
    Task<bool> IsAssignedToRoleAsync(Guid roleId, Guid permissionId, CancellationToken ct);
}
