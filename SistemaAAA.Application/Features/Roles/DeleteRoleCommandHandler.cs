using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Handler que elimina un rol.
/// </summary>
public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, Result<bool>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<DeleteRoleCommandHandler> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="DeleteRoleCommandHandler"/>.
    /// </summary>
    public DeleteRoleCommandHandler(
        IRoleRepository roleRepository,
        IAuditRepository auditRepository,
        ILogger<DeleteRoleCommandHandler> logger)
    {
        _roleRepository = roleRepository;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la eliminación de un rol.
    /// </summary>
    public async Task<Result<bool>> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
            if (role is null)
            {
                _logger.LogWarning("Attempt to delete non-existent role {RoleId}", request.RoleId);
                return Result<bool>.Failure("ROLE_NOT_FOUND", "Rol no encontrado");
            }

            if (role.IsSystem)
            {
                _logger.LogWarning("Attempt to delete system role {RoleId}", request.RoleId);
                return Result<bool>.Failure("CANNOT_DELETE_SYSTEM_ROLE", "Los roles de sistema no pueden eliminarse");
            }

            await _roleRepository.DeleteAsync(request.RoleId, cancellationToken);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.DeletedByUserId,
                EventType = "ROLE_DELETED",
                Resource = "Role",
                Details = $"Rol {request.RoleId} eliminado",
                IpAddress = request.IpAddress,
                CreatedAt = DateTime.UtcNow
            };

            await _auditRepository.InsertAsync(auditLog, cancellationToken);

            _logger.LogInformation("Role {RoleId} deleted by admin {AdminId}", request.RoleId, request.DeletedByUserId);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId}", request.RoleId);
            return Result<bool>.Failure("INTERNAL_ERROR", "Error eliminando rol");
        }
    }
}
