using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Comando para actualizar un usuario existente.
/// </summary>
public record UpdateUserCommand(
    Guid UserId,
    Guid RequestingUserId,
    string? Email,
    string? IpAddress) : IRequest<Result<bool>>;
