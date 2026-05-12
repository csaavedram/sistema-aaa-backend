using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Command para cerrar sesión (logout).
/// </summary>
public record LogoutCommand(Guid UserId, string? AccessToken, string? RefreshToken, string? IpAddress) : IRequest<Result<bool>>;
