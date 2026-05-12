using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Features.Roles;

namespace SistemaAAA.API.Controllers;

/// <summary>
/// Controller for role management operations.
/// Base authorization: Admin role required for all endpoints.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Admin")]
public class RolesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<RolesController> _logger;

    public RolesController(IMediator mediator, ILogger<RolesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    private string? GetIpAddress() => HttpContext?.Connection?.RemoteIpAddress?.ToString();

    private Guid GetRequestingUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim))
        {
            throw new InvalidOperationException("User ID claim not found");
        }
        return Guid.Parse(userIdClaim);
    }

    /// <summary>
    /// POST /api/v1/roles
    /// Create a new role.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), 201)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 409)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        var createdByUserId = GetRequestingUserId();
        var ip = GetIpAddress() ?? string.Empty;

        var cmd = new CreateRoleCommand(
            request.Name ?? string.Empty,
            request.Description ?? string.Empty,
            createdByUserId,
            ip);

        var result = await _mediator.Send(cmd);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ROLE_NAME_TAKEN" => Conflict(result),
                _ => StatusCode(400, result)
            };
        }

        var response = result.Value!;
        return CreatedAtAction(nameof(GetUserRoles), new { userId = "temp" }, new { success = true, data = response });
    }

    /// <summary>
    /// DELETE /api/v1/roles/{id}
    /// Delete a role.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> DeleteRole([FromRoute] Guid id)
    {
        var deletedByUserId = GetRequestingUserId();
        var ip = GetIpAddress();

        var cmd = new DeleteRoleCommand(id, deletedByUserId, ip);

        var result = await _mediator.Send(cmd);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "CANNOT_DELETE_SYSTEM_ROLE" => StatusCode(400, result),
                "ROLE_NOT_FOUND" => NotFound(result),
                _ => StatusCode(400, result)
            };
        }

        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/roles/{roleId}/users/{userId}
    /// Assign a role to a user.
    /// </summary>
    [HttpPost("{roleId:guid}/users/{userId:guid}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 409)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> AssignRoleToUser([FromRoute] Guid roleId, [FromRoute] Guid userId)
    {
        var assignedByUserId = GetRequestingUserId();
        var ip = GetIpAddress();

        var cmd = new AssignRoleToUserCommand(userId, roleId, assignedByUserId, ip);

        var result = await _mediator.Send(cmd);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ROLE_ALREADY_ASSIGNED" => Conflict(result),
                "ROLE_NOT_FOUND" or "USER_NOT_FOUND" => NotFound(result),
                _ => StatusCode(400, result)
            };
        }

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// DELETE /api/v1/roles/{roleId}/users/{userId}
    /// Remove a role from a user.
    /// </summary>
    [HttpDelete("{roleId:guid}/users/{userId:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> RemoveRoleFromUser([FromRoute] Guid roleId, [FromRoute] Guid userId)
    {
        var removedByUserId = GetRequestingUserId();
        var ip = GetIpAddress();

        var cmd = new RemoveRoleFromUserCommand(userId, roleId, removedByUserId, ip);

        var result = await _mediator.Send(cmd);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ROLE_NOT_FOUND" or "USER_NOT_FOUND" => NotFound(result),
                _ => StatusCode(400, result)
            };
        }

        return NoContent();
    }

    /// <summary>
    /// GET /api/v1/roles/users/{userId}
    /// Get all roles assigned to a user.
    /// </summary>
    [HttpGet("users/{userId:guid}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> GetUserRoles([FromRoute] Guid userId)
    {
        var query = new GetUserRolesQuery(userId);

        var result = await _mediator.Send(query);

        if (!result.IsSuccess)
        {
            return StatusCode(404, result);
        }

        var response = result.Value!;
        return Ok(new { success = true, data = response });
    }

    // --- Request DTOs ---
    public record CreateRoleRequest(string Name, string Description);
}
