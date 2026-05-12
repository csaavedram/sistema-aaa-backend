using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Query para obtener los roles de un usuario.
/// </summary>
public record GetUserRolesQuery(Guid UserId) : IRequest<Result<List<RoleDto>>>;
