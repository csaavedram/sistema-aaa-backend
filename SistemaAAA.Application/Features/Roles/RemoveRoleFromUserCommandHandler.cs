using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Handler que remueve un rol de un usuario.
/// </summary>
public class RemoveRoleFromUserCommandHandler : IRequestHandler<RemoveRoleFromUserCommand, Result<bool>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<RemoveRoleFromUserCommandHandler> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="RemoveRoleFromUserCommandHandler"/>.
    /// </summary>
    public RemoveRoleFromUserCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IAuditRepository auditRepository,
        ILogger<RemoveRoleFromUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la remoción de un rol de un usuario.
    /// </summary>
    public async Task<Result<bool>> Handle(RemoveRoleFromUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user is null)
            {
                return Result<bool>.Failure("USER_NOT_FOUND", "Usuario no encontrado");
            }

            var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
            if (role is null)
            {
                return Result<bool>.Failure("ROLE_NOT_FOUND", "Rol no encontrado");
            }

            var isAssigned = await _roleRepository.IsAssignedToUserAsync(request.RoleId, request.UserId, cancellationToken);
            if (!isAssigned)
            {
                return Result<bool>.Failure("ROLE_NOT_ASSIGNED", "El usuario no tiene ese rol");
            }

            await _roleRepository.RemoveFromUserAsync(request.RoleId, request.UserId, cancellationToken);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.RemovedByUserId,
                EventType = "ROLE_REMOVED_FROM_USER",
                Resource = "Role",
                Details = $"Rol {request.RoleId} removido de usuario {request.UserId}",
                IpAddress = request.IpAddress ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _auditRepository.InsertAsync(auditLog, cancellationToken);

            _logger.LogInformation("Role {RoleId} removed from user {UserId} by admin {AdminId}", request.RoleId, request.UserId, request.RemovedByUserId);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role {RoleId} from user {UserId}", request.RoleId, request.UserId);
            return Result<bool>.Failure("INTERNAL_ERROR", "Error removiendo rol");
        }
    }
}
