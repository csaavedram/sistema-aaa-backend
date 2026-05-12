using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SistemaAAA.Domain;

namespace SistemaAAA.Domain.Interfaces;

/// <summary>
/// Repositorio para persistir y consultar registros de auditoría.
/// NOTA: AuditLog es inmutable — no hay operaciones de actualización o borrado.
/// </summary>
public interface IAuditRepository
{
    /// <summary>
    /// Inserta un registro de auditoría.
    /// </summary>
    Task InsertAsync(AuditLog log, CancellationToken ct);

    /// <summary>
    /// Obtiene un registro de auditoría por su identificador.
    /// </summary>
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Obtiene registros de auditoría según un filtro.
    /// </summary>
    Task<IEnumerable<AuditLog>> GetLogsAsync(AuditLogFilter filter, CancellationToken ct);
}
