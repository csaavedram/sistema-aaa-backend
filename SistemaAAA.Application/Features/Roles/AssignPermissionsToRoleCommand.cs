using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Comando para asignar múltiples permisos a un rol.
/// </summary>
public record AssignPermissionsToRoleCommand(
    Guid RoleId,
    List<Guid> PermissionIds,
    Guid AssignedByUserId,
    string? IpAddress) : IRequest<Result<bool>>;
