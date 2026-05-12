using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Command para login.
/// </summary>
public record LoginCommand(string Email, string Password, string IpAddress) : IRequest<Result<AuthResponse>>;

/// <summary>
/// Respuesta del proceso de autenticación.
/// </summary>
public record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn, Guid UserId, string[] Roles);
