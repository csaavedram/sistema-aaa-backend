using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Comando para eliminar (desactivar) un usuario.
/// </summary>
public record DeleteUserCommand(
    Guid UserId,
    Guid RequestingUserId,
    string? IpAddress) : IRequest<Result<bool>>;
