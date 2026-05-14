using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Audit;

/// <summary>
/// Query para obtener un registro de auditoría por su identificador.
/// </summary>
public record GetAuditLogByIdQuery(
    Guid AuditLogId,
    Guid RequestingUserId,
    string? IpAddress) : IRequest<Result<AuditLogDto>>;
