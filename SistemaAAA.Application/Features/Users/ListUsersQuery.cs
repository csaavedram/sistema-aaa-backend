using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Query para obtener un listado paginado de usuarios.
/// </summary>
public record ListUsersQuery(int Page = 1, int PageSize = 20) : IRequest<Result<ListUsersResponse>>;

/// <summary>
/// Respuesta con listado paginado de usuarios.
/// </summary>
public record ListUsersResponse(
    IEnumerable<UserDto> Users,
    int Page,
    int PageSize);
