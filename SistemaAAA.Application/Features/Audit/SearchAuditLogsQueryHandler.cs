using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Audit;

/// <summary>
/// Handler que busca registros de auditoría con filtros y paginación.
/// </summary>
public class SearchAuditLogsQueryHandler : IRequestHandler<SearchAuditLogsQuery, Result<SearchAuditLogsResponse>>
{
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<SearchAuditLogsQueryHandler> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="SearchAuditLogsQueryHandler"/>.
    /// </summary>
    public SearchAuditLogsQueryHandler(
        IAuditRepository auditRepository,
        ILogger<SearchAuditLogsQueryHandler> logger)
    {
        _auditRepository = auditRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la búsqueda de registros de auditoría.
    /// </summary>
    public async Task<Result<SearchAuditLogsResponse>> Handle(SearchAuditLogsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var page = request.Page > 0 ? request.Page : 1;
            var pageSize = request.PageSize > 200 ? 200 : request.PageSize;
            if (pageSize <= 0)
            {
                pageSize = 50;
            }

            var filter = new AuditLogFilter
            {
                UserId = request.UserId,
                EventType = request.EventType,
                Resource = request.Resource,
                From = request.From,
                To = request.To,
                Page = page,
                PageSize = pageSize
            };

            var logs = await _auditRepository.GetLogsAsync(filter, cancellationToken);

            var response = new SearchAuditLogsResponse(
                logs.Select(log => new AuditLogDto(
                    log.Id,
                    log.UserId,
                    log.EventType,
                    log.Resource,
                    log.Details,
                    log.IpAddress,
                    log.CreatedAt)),
                page,
                pageSize);

            var accessLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.RequestingUserId,
                EventType = "AUDIT_LOG_ACCESSED",
                Resource = "Audit",
                Details = $"Consulta con filtros: EventType={request.EventType}, From={request.From}",
                IpAddress = request.IpAddress ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _auditRepository.InsertAsync(accessLog, cancellationToken);

            _logger.LogInformation("Audit logs searched with page {Page} and page size {PageSize}", page, pageSize);
            return Result<SearchAuditLogsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching audit logs");
            return Result<SearchAuditLogsResponse>.Failure("INTERNAL_ERROR", "Error buscando logs de auditoría");
        }
    }
}
