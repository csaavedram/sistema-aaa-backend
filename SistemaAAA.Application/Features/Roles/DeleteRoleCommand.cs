using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Comando para eliminar un rol.
/// </summary>
public record DeleteRoleCommand(
    Guid RoleId,
    Guid DeletedByUserId,
    string? IpAddress) : IRequest<Result<bool>>;
