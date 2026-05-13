using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Command para cambiar la contraseña del usuario autenticado.
/// </summary>
public record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword,
    string? IpAddress) : IRequest<Result<bool>>;