using MediatR;
using SistemaAAA.Application.Common;
using SistemaAAA.Application.Features.Common;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Consulta para obtener todos los permisos asignados a un rol.
/// </summary>
public record GetRolePermissionsQuery(Guid RoleId) : IRequest<Result<List<PermissionDto>>>;
