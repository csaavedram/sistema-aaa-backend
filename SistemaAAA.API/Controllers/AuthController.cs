using System;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Application.Features.Auth;

namespace SistemaAAA.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "refresh_token";
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IMediator mediator, ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    private string? GetIpAddress() =>
        HttpContext?.Connection?.RemoteIpAddress?.ToString();

    private static CookieOptions BuildRefreshTokenCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Expires = DateTime.UtcNow.AddDays(7)
    };

    /// <summary>
    /// POST /login
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 423)]
    [ProducesResponseType(typeof(object), 429)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ip = GetIpAddress() ?? string.Empty;
        var cmd = new LoginCommand(request.Email ?? string.Empty, request.Password ?? string.Empty, ip);

        var result = await _mediator.Send(cmd);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "ACCOUNT_LOCKED") return StatusCode(423, result);
            if (result.ErrorCode == "AUTH_INVALID_CREDENTIALS") return Unauthorized(result);
            return StatusCode(401, result);
        }

        var auth = result.Value!;
        Response.Cookies.Append(RefreshTokenCookieName, auth.RefreshToken, BuildRefreshTokenCookieOptions());

        var body = new
        {
            success = true,
            data = new
            {
                accessToken = auth.AccessToken,
                expiresIn = auth.ExpiresIn,
                userId = auth.UserId,
                roles = auth.Roles
            }
        };

        return Ok(body);
    }

    /// <summary>
    /// POST /refresh
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new { errorCode = "TOKEN_MISSING", message = "Refresh token no encontrado" });
        }

        var ip = GetIpAddress();
        var cmd = new RefreshTokenCommand { RefreshToken = refreshToken, IpAddress = ip };

        var result = await _mediator.Send(cmd);
        if (!result.IsSuccess)
        {
            Response.Cookies.Delete(RefreshTokenCookieName);
            return StatusCode(401, result);
        }

        var auth = result.Value!;
        Response.Cookies.Append(RefreshTokenCookieName, auth.RefreshToken, BuildRefreshTokenCookieOptions());

        var body = new
        {
            success = true,
            data = new
            {
                accessToken = auth.AccessToken,
                expiresIn = auth.ExpiresIn
            }
        };

        return Ok(body);
    }

    /// <summary>
    /// POST /logout
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var ip = GetIpAddress();
            var authorizationHeader = Request.Headers["Authorization"].ToString();
            var accessToken = authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authorizationHeader[7..]
                : authorizationHeader;
            var refreshToken = Request.Cookies[RefreshTokenCookieName];

            var cmd = new LogoutCommand(userId, string.IsNullOrWhiteSpace(accessToken) ? null : accessToken, string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken, ip);

            var result = await _mediator.Send(cmd);

            if (!result.IsSuccess)
            {
                return StatusCode(500, result);
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, Result<bool>.Failure("INTERNAL_ERROR", "Error processing logout"));
        }
        finally
        {
            Response.Cookies.Delete(RefreshTokenCookieName);
        }
    }

    /// <summary>
    /// POST /change-password
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 401)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 422)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null)
        {
            return Unauthorized();
        }

        var cmd = new ChangePasswordCommand(
            UserId: Guid.Parse(userIdClaim),
            CurrentPassword: request.CurrentPassword,
            NewPassword: request.NewPassword,
            IpAddress: GetIpAddress());

        var result = await _mediator.Send(cmd);

        if (result.IsSuccess)
        {
            return Ok(new { success = true, message = "Contraseña actualizada correctamente" });
        }

        return result.ErrorCode switch
        {
            "CURRENT_PASSWORD_INVALID" => BadRequest(result),
            "SAME_PASSWORD" => BadRequest(result),
            "WEAK_PASSWORD" => StatusCode(422, result),
            "USER_NOT_FOUND" => NotFound(result),
            _ => StatusCode(500, result)
        };
    }

    /// <summary>
    /// POST /forgot-password
    /// Always returns 200 with a generic message (anti-enumeration).
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 429)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var ip = GetIpAddress();
        var cmd = new ForgotPasswordCommand(request.Email ?? string.Empty, ip);

        try
        {
            await _mediator.Send(cmd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot-password request");
        }

        return Ok(new { success = true, message = "Si el email existe, recibirás instrucciones en breve" });
    }

    /// <summary>
    /// POST /reset-password
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 422)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var ip = GetIpAddress();
        var cmd = new ResetPasswordCommand(request.Token ?? string.Empty, request.NewPassword ?? string.Empty, ip);

        var result = await _mediator.Send(cmd);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode is "TOKEN_EXPIRED" or "TOKEN_INVALID")
            {
                return BadRequest(result);
            }

            if (result.ErrorCode == "WEAK_PASSWORD")
            {
                return StatusCode(422, result);
            }

            return BadRequest(result);
        }

        return Ok(new { success = true, message = "Contraseña actualizada correctamente" });
    }

    // --- Request DTOs ---
    public record LoginRequest(string Email, string Password);
    public record ForgotPasswordRequest(string Email);
    public record ResetPasswordRequest(string Token, string NewPassword);
    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
}