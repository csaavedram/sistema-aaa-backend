using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Features.Audit;

namespace SistemaAAA.API.Controllers;

/// <summary>
/// Controller for audit log operations.
/// Base authorization: Admin role required for all endpoints.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Admin")]
public class AuditController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IMediator mediator, ILogger<AuditController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/v1/audit
    /// Search audit logs with optional filters and pagination.
    /// Query parameters: userId, eventType, resource, from, to, page, pageSize
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "audit.read")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> SearchAuditLogs(
        [FromQuery] Guid? userId,
        [FromQuery] string? eventType,
        [FromQuery] string? resource,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = new SearchAuditLogsQuery(
            userId,
            eventType,
            resource,
            from,
            to,
            page,
            pageSize);

        var result = await _mediator.Send(query);

        if (!result.IsSuccess)
        {
            return StatusCode(500, result);
        }

        var response = result.Value!;
        return Ok(new { success = true, data = response });
    }

    /// <summary>
    /// GET /api/v1/audit/{id}
    /// Get an audit log by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "audit.read")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> GetAuditLogById([FromRoute] Guid id)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var requestingUserId))
            return Unauthorized();
        var query = new GetAuditLogByIdQuery(id, requestingUserId);

        var result = await _mediator.Send(query);

        if (!result.IsSuccess)
        {
            return StatusCode(404, result);
        }

        var response = result.Value!;
        return Ok(new { success = true, data = response });
    }
}
