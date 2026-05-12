using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Handler que asigna un rol a un usuario.
/// </summary>
public class AssignRoleToUserCommandHandler : IRequestHandler<AssignRoleToUserCommand, Result<bool>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<AssignRoleToUserCommandHandler> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="AssignRoleToUserCommandHandler"/>.
    /// </summary>
    public AssignRoleToUserCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IAuditRepository auditRepository,
        ILogger<AssignRoleToUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la asignación de un rol a un usuario.
    /// </summary>
    public async Task<Result<bool>> Handle(AssignRoleToUserCommand request, CancellationToken cancellationToken)
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

            var alreadyAssigned = await _roleRepository.IsAssignedToUserAsync(request.RoleId, request.UserId, cancellationToken);
            if (alreadyAssigned)
            {
                return Result<bool>.Failure("ROLE_ALREADY_ASSIGNED", "El usuario ya tiene ese rol");
            }

            await _roleRepository.AssignToUserAsync(request.RoleId, request.UserId, request.AssignedByUserId, cancellationToken);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.AssignedByUserId,
                EventType = "ROLE_ASSIGNED_TO_USER",
                Resource = "Role",
                Details = $"Rol {request.RoleId} asignado a usuario {request.UserId}",
                IpAddress = request.IpAddress,
                CreatedAt = DateTime.UtcNow
            };

            await _auditRepository.InsertAsync(auditLog, cancellationToken);

            _logger.LogInformation("Role {RoleId} assigned to user {UserId} by admin {AdminId}", request.RoleId, request.UserId, request.AssignedByUserId);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {RoleId} to user {UserId}", request.RoleId, request.UserId);
            return Result<bool>.Failure("INTERNAL_ERROR", "Error asignando rol");
        }
    }
}
