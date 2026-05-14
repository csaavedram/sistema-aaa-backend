using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Handler que asigna múltiples permisos a un rol.
/// </summary>
public class AssignPermissionsToRoleCommandHandler : IRequestHandler<AssignPermissionsToRoleCommand, Result<bool>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<AssignPermissionsToRoleCommandHandler> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="AssignPermissionsToRoleCommandHandler"/>.
    /// </summary>
    public AssignPermissionsToRoleCommandHandler(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IAuditRepository auditRepository,
        ILogger<AssignPermissionsToRoleCommandHandler> logger)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la asignación de permisos a un rol.
    /// </summary>
    public async Task<Result<bool>> Handle(AssignPermissionsToRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Verificar que el rol existe
            var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
            if (role is null)
            {
                return Result<bool>.Failure("ROLE_NOT_FOUND", "Rol no encontrado");
            }

            var assignedCount = 0;

            // Validar y asignar cada permiso
            foreach (var permissionId in request.PermissionIds)
            {
                // Verificar que el permiso existe
                var permission = await _permissionRepository.GetByIdAsync(permissionId, cancellationToken);
                if (permission is null)
                {
                    return Result<bool>.Failure(
                        "PERMISSION_NOT_FOUND",
                        $"Permiso con ID {permissionId} no encontrado");
                }

                // Verificar si ya está asignado — idempotente, omitir silenciosamente
                var isAssigned = await _permissionRepository.IsAssignedToRoleAsync(
                    request.RoleId,
                    permissionId,
                    cancellationToken);

                if (isAssigned)
                {
                    _logger.LogWarning(
                        "Permission {PermissionId} already assigned to role {RoleId}",
                        permissionId,
                        request.RoleId);
                    continue;
                }

                // Asignar el permiso
                await _permissionRepository.AssignToRoleAsync(
                    request.RoleId,
                    permissionId,
                    cancellationToken);

                assignedCount++;
            }

            // Registrar en auditoría
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.AssignedByUserId,
                EventType = "PERMISSIONS_ASSIGNED_TO_ROLE",
                Resource = "Permission",
                Details = $"{assignedCount} permisos asignados al rol {request.RoleId}",
                IpAddress = request.IpAddress ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _auditRepository.InsertAsync(auditLog, cancellationToken);

            _logger.LogInformation(
                "{Count} permissions assigned to role {RoleId} by user {UserId}",
                assignedCount,
                request.RoleId,
                request.AssignedByUserId);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error assigning permissions to role {RoleId}",
                request.RoleId);
            return Result<bool>.Failure("INTERNAL_ERROR", "Error al asignar permisos");
        }
    }
}
