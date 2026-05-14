using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Audit;

/// <summary>
/// Query para buscar registros de auditoría con filtros y paginación.
/// </summary>
public record SearchAuditLogsQuery(
    Guid? UserId,
    string? EventType,
    string? Resource,
    DateTime? From,
    DateTime? To,
    Guid RequestingUserId,
    string? IpAddress,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<SearchAuditLogsResponse>>;

/// <summary>
/// DTO de un registro de auditoría seguro para exposición.
/// </summary>
public record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string EventType,
    string Resource,
    string? Details,
    string? IpAddress,
    DateTime CreatedAt);

/// <summary>
/// Respuesta de búsqueda de auditoría.
/// </summary>
public record SearchAuditLogsResponse(
    IEnumerable<AuditLogDto> Logs,
    int Page,
    int PageSize);
