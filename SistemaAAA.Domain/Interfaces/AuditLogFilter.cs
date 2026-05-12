using System;

namespace SistemaAAA.Domain.Interfaces;

/// <summary>
/// DTO para filtrar consultas de AuditLog.
/// Se puede extender según necesidades de paginación y búsquedas.
/// </summary>
public class AuditLogFilter
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public Guid? UserId { get; set; }
    public string? EventType { get; set; }
    public string? Resource { get; set; }

    /// <summary>
    /// Página (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Tamaño de página.
    /// </summary>
    public int PageSize { get; set; } = 50;
}
