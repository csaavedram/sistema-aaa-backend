using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Application.Features.Common;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Handler que obtiene todos los permisos de un usuario.
/// Suma los permisos de todos los roles asignados al usuario.
/// </summary>
public class GetUserPermissionsQueryHandler : IRequestHandler<GetUserPermissionsQuery, Result<List<PermissionDto>>>
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly ILogger<GetUserPermissionsQueryHandler> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="GetUserPermissionsQueryHandler"/>.
    /// </summary>
    public GetUserPermissionsQueryHandler(
        IPermissionRepository permissionRepository,
        ILogger<GetUserPermissionsQueryHandler> logger)
    {
        _permissionRepository = permissionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la consulta de permisos de un usuario.
    /// </summary>
    public async Task<Result<List<PermissionDto>>> Handle(GetUserPermissionsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Obtener los permisos del usuario (suma de todos sus roles)
            var permissions = await _permissionRepository.GetByUserIdAsync(
                request.UserId,
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
                "Retrieved {Count} permissions for user {UserId}",
                permissionDtos.Count,
                request.UserId);

            return Result<List<PermissionDto>>.Success(permissionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving permissions for user {UserId}",
                request.UserId);
            return Result<List<PermissionDto>>.Failure("INTERNAL_ERROR", "Error al obtener permisos");
        }
    }
}
