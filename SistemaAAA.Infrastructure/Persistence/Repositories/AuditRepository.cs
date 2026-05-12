using Microsoft.EntityFrameworkCore;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación del repositorio de auditoría.
/// CRÍTICO: Esta clase solo implementa INSERT y SELECT. No permite UPDATE ni DELETE.
/// </summary>
public class AuditRepository : IAuditRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="AuditRepository"/>.
    /// </summary>
    /// <param name="context">Contexto de base de datos.</param>
    public AuditRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Inserta un nuevo registro de auditoría en la base de datos.
    /// </summary>
    /// <param name="log">Registro de auditoría a insertar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <exception cref="ArgumentNullException">Si log es nulo.</exception>
    public async Task InsertAsync(AuditLog log, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(log, nameof(log));

        await _context.AuditLogs.AddAsync(log, ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Obtiene registros de auditoría según el filtro proporcionado.
    /// Los resultados se consultan sin tracking para no permitir cambios.
    /// </summary>
    /// <param name="filter">Filtro de consulta con criterios opcionales (fechas, usuario, tipo evento, etc.).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Enumerable de registros de auditoría que coinciden con el filtro.</returns>
    public async Task<IEnumerable<AuditLog>> GetLogsAsync(AuditLogFilter filter, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter, nameof(filter));

        var query = _context.AuditLogs.AsNoTracking();

        if (filter.From.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= filter.To.Value);
        }

        if (filter.UserId.HasValue)
        {
            query = query.Where(x => x.UserId == filter.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.EventType))
        {
            query = query.Where(x => x.EventType == filter.EventType);
        }

        if (!string.IsNullOrWhiteSpace(filter.Resource))
        {
            query = query.Where(x => x.Resource == filter.Resource);
        }

        var page = filter.Page > 0 ? filter.Page : 1;
        var pageSize = filter.PageSize > 0 ? filter.PageSize : 50;

        var logs = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return logs;
    }
}
