using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Application.Features.Common;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Handler que obtiene todos los permisos asignados a un rol.
/// </summary>
public class GetRolePermissionsQueryHandler : IRequestHandler<GetRolePermissionsQuery, Result<List<PermissionDto>>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly ILogger<GetRolePermissionsQueryHandler> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="GetRolePermissionsQueryHandler"/>.
    /// </summary>
    public GetRolePermissionsQueryHandler(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        ILogger<GetRolePermissionsQueryHandler> logger)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la consulta de permisos de un rol.
    /// </summary>
    public async Task<Result<List<PermissionDto>>> Handle(GetRolePermissionsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Verificar que el rol existe
            var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
            if (role is null)
            {
                return Result<List<PermissionDto>>.Failure("ROLE_NOT_FOUND", "Rol no encontrado");
            }

            // Obtener los permisos del rol
            var permissions = await _permissionRepository.GetByRoleIdAsync(
                request.RoleId,
                cancellationToken);

            // Mapear a DTO
            var permissionDtos = permissions.Select(p => new PermissionDto
            {
                Id = p.Id,
                Name = p.Name,
                Resource = p.Resource,
                Action = p.Action
            }).ToList();

            _logger.LogInformation(
                "Retrieved {Count} permissions for role {RoleId}",
                permissionDtos.Count,
                request.RoleId);

            return Result<List<PermissionDto>>.Success(permissionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving permissions for role {RoleId}",
                request.RoleId);
            return Result<List<PermissionDto>>.Failure("INTERNAL_ERROR", "Error al obtener permisos");
        }
    }
}
