using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Handler que elimina (desactiva) un usuario del sistema.
/// </summary>
public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Result<bool>>
{
    private readonly IUserRepository _userRepository;
    private readonly IAuthRepository _authRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<DeleteUserCommandHandler> _logger;

    /// <summary>
    /// Constructor con dependencias requeridas.
    /// </summary>
    public DeleteUserCommandHandler(
        IUserRepository userRepository,
        IAuthRepository authRepository,
        IAuditRepository auditRepository,
        ILogger<DeleteUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _authRepository = authRepository;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la eliminación (soft delete) de un usuario.
    /// </summary>
    public async Task<Result<bool>> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Paso 1: Obtener usuario
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user is null)
            {
                _logger.LogWarning("Attempt to delete non-existent user {UserId}", request.UserId);
                return Result<bool>.Failure("USER_NOT_FOUND", "Usuario no encontrado");
            }

            // Paso 2: Regla de negocio — no eliminar último admin
            var userRoles = await _authRepository.GetUserRolesAsync(request.UserId, cancellationToken);
            if (userRoles.Contains("Admin"))
            {
                var adminCount = await _userRepository.GetAdminCountAsync(cancellationToken);
                if (adminCount == 1)
                {
                    _logger.LogWarning("Attempt to delete last admin user {UserId}", request.UserId);
                    return Result<bool>.Failure("CANNOT_DELETE_LAST_ADMIN", "No se puede eliminar el último administrador");
                }
            }

            // Paso 3: Ejecutar soft delete
            await _userRepository.DeleteAsync(request.UserId, cancellationToken);

            // Paso 4: Auditar evento
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.RequestingUserId,
                EventType = "USER_DEACTIVATED",
                Resource = "User",
                Details = $"Usuario {request.UserId} desactivado",
                IpAddress = request.IpAddress ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _auditRepository.InsertAsync(auditLog, cancellationToken);

            // Paso 5: Log de alerta
            _logger.LogWarning("User {UserId} deactivated by admin {AdminId}", request.UserId, request.RequestingUserId);

            // Paso 6: Retornar éxito
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", request.UserId);
            return Result<bool>.Failure("INTERNAL_ERROR", "Error eliminando usuario");
        }
    }
}
