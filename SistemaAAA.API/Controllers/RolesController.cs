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

    private string? GetIpAddress() =>
        HttpContext?.Connection?.RemoteIpAddress?.ToString();

    /// <summary>
    /// POST /api/v1/roles
    /// Create a new role.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "roles.create")]
    [ProducesResponseType(typeof(object), 201)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 409)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var createdByUserId))
            return Unauthorized();
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
        return StatusCode(201, new { success = true, data = response });
    }

    /// <summary>
    /// DELETE /api/v1/roles/{id}
    /// Delete a role.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "roles.delete")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> DeleteRole([FromRoute] Guid id)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var deletedByUserId))
            return Unauthorized();
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
    [Authorize(Policy = "roles.assign")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 409)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> AssignRoleToUser([FromRoute] Guid roleId, [FromRoute] Guid userId)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var assignedByUserId))
            return Unauthorized();
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
    [Authorize(Policy = "roles.assign")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> RemoveRoleFromUser([FromRoute] Guid roleId, [FromRoute] Guid userId)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var removedByUserId))
            return Unauthorized();
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

    /// <summary>
    /// POST /api/v1/roles/{id}/permissions
    /// Assign multiple permissions to a role.
    /// </summary>
    [HttpPost("{id:guid}/permissions")]
    [Authorize(Policy = "permissions.assign")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> AssignPermissionsToRole([FromRoute] Guid id, [FromBody] AssignPermissionsRequest request)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var assignedByUserId))
            return Unauthorized();
        var ip = GetIpAddress();

        var cmd = new AssignPermissionsToRoleCommand(id, request.PermissionIds ?? [], assignedByUserId, ip);

        var result = await _mediator.Send(cmd);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ROLE_NOT_FOUND" => NotFound(result),
                "PERMISSION_NOT_FOUND" => NotFound(result),
                _ => StatusCode(400, result)
            };
        }

        return Ok(new { success = true, message = "Permisos asignados" });
    }

    /// <summary>
    /// GET /api/v1/roles/{id}/permissions
    /// Get all permissions assigned to a role.
    /// </summary>
    [HttpGet("{id:guid}/permissions")]
    [Authorize(Policy = "roles.read")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> GetRolePermissions([FromRoute] Guid id)
    {
        var query = new GetRolePermissionsQuery(id);

        var result = await _mediator.Send(query);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ROLE_NOT_FOUND" => NotFound(result),
                _ => StatusCode(400, result)
            };
        }

        var response = result.Value!;
        return Ok(new { success = true, data = response });
    }

    /// <summary>
    /// GET /api/v1/roles
    /// Get all roles.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "roles.read")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> GetRoles()
    {
        var query = new GetRolesQuery();

        var result = await _mediator.Send(query);

        if (!result.IsSuccess)
        {
            return StatusCode(500, result);
        }

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// GET /api/v1/roles/{id}
    /// Get a role by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "roles.read")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> GetRoleById([FromRoute] Guid id)
    {
        var query = new GetRoleByIdQuery(id);

        var result = await _mediator.Send(query);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ROLE_NOT_FOUND" => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(new { success = true, data = result.Value });
    }

    // --- Request DTOs ---
    public record CreateRoleRequest(string Name, string Description);
    public record AssignPermissionsRequest(List<Guid> PermissionIds);
}