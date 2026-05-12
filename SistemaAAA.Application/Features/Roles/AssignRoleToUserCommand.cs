using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Comando para asignar un rol a un usuario.
/// </summary>
public record AssignRoleToUserCommand(
    Guid UserId,
    Guid RoleId,
    Guid AssignedByUserId,
    string? IpAddress) : IRequest<Result<bool>>;
