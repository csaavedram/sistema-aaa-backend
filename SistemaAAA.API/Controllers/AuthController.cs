using System.Security.Claims;
using System.Threading;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Application.Features.Auth;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;
    private readonly IJwtService _jwtService;
    private readonly IAuthRepository _authRepository;

    public AuthController(IMediator mediator, ILogger<AuthController> logger, IJwtService jwtService, IAuthRepository authRepository)
    {
        _mediator = mediator;
        _logger = logger;
        _jwtService = jwtService;
        _authRepository = authRepository;
    }

    private string? GetIpAddress() => HttpContext?.Connection?.RemoteIpAddress?.ToString();

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
            // Map known error codes to HTTP statuses
            if (result.ErrorCode == "ACCOUNT_LOCKED") return StatusCode(423, result);
            if (result.ErrorCode == "AUTH_INVALID_CREDENTIALS") return Unauthorized(result);
            // fallback
            return StatusCode(result is null ? 401 : 401, result);
        }

        // Success: set refresh token cookie (httpOnly)
        var auth = result.Value!;

        var newRefreshToken = _jwtService.GenerateRefreshToken(auth.UserId);
        await _authRepository.SaveRefreshTokenAsync(auth.UserId, newRefreshToken, ip ?? string.Empty, HttpContext?.RequestAborted ?? CancellationToken.None);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7)
        };

        Response.Cookies.Append("refresh_token", newRefreshToken, cookieOptions);

        // Return access token info (NOT including refresh token)
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
        var refreshToken = Request.Cookies["refresh_token"];
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new { errorCode = "TOKEN_MISSING", message = "Refresh token no encontrado" });
        }

        var ip = GetIpAddress();
        var cmd = new RefreshTokenCommand { RefreshToken = refreshToken };

        var result = await _mediator.Send(cmd);
        if (!result.IsSuccess)
        {
            // Clear cookie on failure
            Response.Cookies.Delete("refresh_token");
            return StatusCode(result is null ? 401 : 401, result);
        }

        // Handler returns access token string. Extract user id from token to persist new refresh token.
        var accessToken = result.Value!;
        if (!_jwtService.ValidateToken(accessToken, out var principal) || principal is null)
        {
            Response.Cookies.Delete("refresh_token");
            return StatusCode(401, new { error = "INVALID_TOKEN" });
        }

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim))
        {
            Response.Cookies.Delete("refresh_token");
            return StatusCode(401, new { error = "INVALID_TOKEN" });
        }

        var userId = Guid.Parse(userIdClaim);

        var newRefresh = _jwtService.GenerateRefreshToken(userId);
        await _authRepository.SaveRefreshTokenAsync(userId, newRefresh, ip ?? string.Empty, HttpContext?.RequestAborted ?? CancellationToken.None);

        var body = new
        {
            success = true,
            data = new
            {
                accessToken = accessToken,
                expiresIn = 60 * 60
            }
        };

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7)
        };

        Response.Cookies.Append("refresh_token", newRefresh, cookieOptions);

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
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim)) return Unauthorized();

        var ip = GetIpAddress();
        var accessToken = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        var refreshToken = Request.Cookies["refresh_token"];

        var cmd = new LogoutCommand(Guid.Parse(userIdClaim), string.IsNullOrWhiteSpace(accessToken) ? null : accessToken, string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken, ip);

        try
        {
            var result = await _mediator.Send(cmd);

            // Always delete cookie
            Response.Cookies.Delete("refresh_token");

            if (!result.IsSuccess)
            {
                return StatusCode(result is null ? 500 : 500, result);
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            Response.Cookies.Delete("refresh_token");
            return StatusCode(500, Result<bool>.Failure("INTERNAL_ERROR", "Error processing logout"));
        }
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
        // Fire-and-forget the command but still await to allow audit in handler
        var cmd = new ForgotPasswordCommand(request.Email ?? string.Empty, ip);

        try
        {
            await _mediator.Send(cmd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot-password request");
            return StatusCode(500, new { success = true, message = "Si el email existe, recibirás instrucciones en breve" });
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
            return StatusCode(result is null ? 400 : 400, result);
        }

        return Ok(new { success = true, message = "Contraseña actualizada correctamente" });
    }

    // --- Request DTOs ---
    public record LoginRequest(string Email, string Password);
    public record ForgotPasswordRequest(string Email);
    public record ResetPasswordRequest(string Token, string NewPassword);
}
