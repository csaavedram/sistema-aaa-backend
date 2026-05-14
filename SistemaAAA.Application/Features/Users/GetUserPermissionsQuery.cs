using MediatR;
using SistemaAAA.Application.Common;
using SistemaAAA.Application.Features.Common;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Consulta para obtener todos los permisos de un usuario.
/// Suma los permisos de todos los roles asignados al usuario.
/// </summary>
public record GetUserPermissionsQuery(Guid UserId) : IRequest<Result<List<PermissionDto>>>;
