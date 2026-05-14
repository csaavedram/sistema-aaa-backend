using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Audit;

/// <summary>
/// Handler que obtiene un registro de auditoría por Id.
/// </summary>
public class GetAuditLogByIdQueryHandler : IRequestHandler<GetAuditLogByIdQuery, Result<AuditLogDto>>
{
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<GetAuditLogByIdQueryHandler> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="GetAuditLogByIdQueryHandler"/>.
    /// </summary>
    public GetAuditLogByIdQueryHandler(
        IAuditRepository auditRepository,
        ILogger<GetAuditLogByIdQueryHandler> logger)
    {
        _auditRepository = auditRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la consulta para obtener un log de auditoría por Id.
    /// </summary>
    public async Task<Result<AuditLogDto>> Handle(GetAuditLogByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var log = await _auditRepository.GetByIdAsync(request.AuditLogId, cancellationToken);
            if (log is null)
            {
                _logger.LogWarning("Audit log {AuditLogId} not found", request.AuditLogId);
                return Result<AuditLogDto>.Failure("AUDIT_LOG_NOT_FOUND", "Registro no encontrado");
            }

            var dto = new AuditLogDto(
                log.Id,
                log.UserId,
                log.EventType,
                log.Resource,
                log.Details,
                log.IpAddress,
                log.CreatedAt);

            var accessLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.RequestingUserId,
                EventType = "AUDIT_LOG_ACCESSED",
                Resource = "Audit",
                Details = $"Acceso a log {request.AuditLogId}",
                IpAddress = request.IpAddress ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _auditRepository.InsertAsync(accessLog, cancellationToken);

            _logger.LogInformation("Audit log {AuditLogId} accessed by {RequestingUserId}", request.AuditLogId, request.RequestingUserId);
            return Result<AuditLogDto>.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit log {AuditLogId}", request.AuditLogId);
            return Result<AuditLogDto>.Failure("INTERNAL_ERROR", "Error obteniendo log de auditoría");
        }
    }
}
