using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SistemaAAA.Domain;

namespace SistemaAAA.Domain.Interfaces;

/// <summary>
/// Repositorio para operaciones CRUD y consultas sobre usuarios.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Obtiene un usuario por su identificador.
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Obtiene una página de usuarios.
    /// </summary>
    Task<IEnumerable<User>> GetAllAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// Crea un nuevo usuario.
    /// </summary>
    Task CreateAsync(User user, CancellationToken ct);

    /// <summary>
    /// Actualiza un usuario existente.
    /// </summary>
    Task UpdateAsync(User user, CancellationToken ct);

    /// <summary>
    /// Elimina un usuario por su identificador.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Comprueba si existe un usuario con el email proporcionado.
    /// </summary>
    Task<bool> ExistsWithEmailAsync(string email, CancellationToken ct);

    /// <summary>
    /// Obtiene el número de administradores (roles de administrador) existentes.
    /// </summary>
    Task<int> GetAdminCountAsync(CancellationToken ct);
}
