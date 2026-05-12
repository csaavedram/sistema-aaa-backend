using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Handler que obtiene los roles de un usuario.
/// </summary>
public class GetUserRolesQueryHandler : IRequestHandler<GetUserRolesQuery, Result<List<RoleDto>>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ILogger<GetUserRolesQueryHandler> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="GetUserRolesQueryHandler"/>.
    /// </summary>
    public GetUserRolesQueryHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        ILogger<GetUserRolesQueryHandler> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la consulta para obtener los roles de un usuario.
    /// </summary>
    public async Task<Result<List<RoleDto>>> Handle(GetUserRolesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user is null)
            {
                return Result<List<RoleDto>>.Failure("USER_NOT_FOUND", "Usuario no encontrado");
            }

            var roles = await _roleRepository.GetRolesForUserAsync(request.UserId, cancellationToken);
            var dtoList = roles
                .Select(role => new RoleDto(role.Id, role.Name, role.Description, role.IsSystem))
                .ToList();

            _logger.LogInformation("Retrieved {Count} roles for user {UserId}", dtoList.Count, request.UserId);
            return Result<List<RoleDto>>.Success(dtoList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles for user {UserId}", request.UserId);
            return Result<List<RoleDto>>.Failure("INTERNAL_ERROR", "Error obteniendo roles del usuario");
        }
    }
}
