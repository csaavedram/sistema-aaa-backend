using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Comando para quitar un rol a un usuario.
/// </summary>
public record RemoveRoleFromUserCommand(
    Guid UserId,
    Guid RoleId,
    Guid RemovedByUserId,
    string? IpAddress) : IRequest<Result<bool>>;
