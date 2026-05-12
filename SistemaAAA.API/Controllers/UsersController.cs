using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Features.Users;

namespace SistemaAAA.API.Controllers;

/// <summary>
/// Controller for user management operations.
/// Base authorization: Admin role required for all endpoints.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IMediator mediator, ILogger<UsersController> logger)
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
    /// POST /api/v1/users
    /// Create a new user.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), 201)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 409)]
    [ProducesResponseType(typeof(object), 422)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var createdByUserId = GetRequestingUserId();
        var ip = GetIpAddress();

        var cmd = new CreateUserCommand(
            request.Email ?? string.Empty,
            request.Password ?? string.Empty,
            createdByUserId,
            ip);

        var result = await _mediator.Send(cmd);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "EMAIL_ALREADY_EXISTS" => Conflict(result),
                "WEAK_PASSWORD" => StatusCode(422, result),
                _ => StatusCode(400, result)
            };
        }

        var response = result.Value!;
        return CreatedAtAction(nameof(GetUser), new { id = response.UserId }, new { success = true, data = response });
    }

    /// <summary>
    /// GET /api/v1/users/{id}
    /// Get a user by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> GetUser([FromRoute] Guid id)
    {
        var query = new GetUserQuery(id);

        var result = await _mediator.Send(query);

        if (!result.IsSuccess)
        {
            return StatusCode(404, result);
        }

        var response = result.Value!;
        return Ok(new { success = true, data = response });
    }

    /// <summary>
    /// PUT /api/v1/users/{id}
    /// Update a user.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 409)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> UpdateUser([FromRoute] Guid id, [FromBody] UpdateUserRequest request)
    {
        var requestingUserId = GetRequestingUserId();
        var ip = GetIpAddress();

        var cmd = new UpdateUserCommand(
            id,
            requestingUserId,
            request.Email,
            ip);

        var result = await _mediator.Send(cmd);

        if (!result.IsSuccess)
        {
            return StatusCode(400, result);
        }

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// DELETE /api/v1/users/{id}
    /// Delete a user (soft delete).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 409)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> DeleteUser([FromRoute] Guid id)
    {
        var requestingUserId = GetRequestingUserId();
        var ip = GetIpAddress();

        var cmd = new DeleteUserCommand(
            id,
            requestingUserId,
            ip);

        var result = await _mediator.Send(cmd);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "CANNOT_DELETE_LAST_ADMIN" => Conflict(result),
                "USER_NOT_FOUND" => NotFound(result),
                _ => StatusCode(400, result)
            };
        }

        return NoContent();
    }

    /// <summary>
    /// GET /api/v1/users
    /// List all users with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> ListUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = new ListUsersQuery(page, pageSize);

        var result = await _mediator.Send(query);

        if (!result.IsSuccess)
        {
            return StatusCode(500, result);
        }

        var response = result.Value!;
        return Ok(new { success = true, data = response });
    }

    // --- Request DTOs ---
    public record CreateUserRequest(string Email, string Password);
    public record UpdateUserRequest(string? Email);
}
