using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Handler que actualiza un usuario existente.
/// </summary>
public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result<bool>>
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<UpdateUserCommandHandler> _logger;

    /// <summary>
    /// Constructor con dependencias requeridas.
    /// </summary>
    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        IAuditRepository auditRepository,
        ILogger<UpdateUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la actualización de un usuario.
    /// </summary>
    public async Task<Result<bool>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Paso 1: Obtener usuario
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user is null)
            {
                _logger.LogWarning("Attempt to update non-existent user {UserId}", request.UserId);
                return Result<bool>.Failure("USER_NOT_FOUND", "Usuario no encontrado");
            }

            // Paso 2: Validar y actualizar email si se proporciona
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                // Verificar que el nuevo email no esté en uso por otro usuario
                var emailExists = await _userRepository.ExistsWithEmailAsync(request.Email, cancellationToken);
                if (emailExists && user.Email != request.Email)
                {
                    _logger.LogWarning("Attempt to update user with existing email: {Email}", "***");
                    return Result<bool>.Failure("EMAIL_ALREADY_EXISTS", "Email ya en uso");
                }

                user.Email = request.Email;
            }

            // Paso 3: Actualizar timestamp
            user.UpdatedAt = DateTime.UtcNow;

            // Paso 4: Persistir cambios
            await _userRepository.UpdateAsync(user, cancellationToken);

            // Paso 5: Auditar evento
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.RequestingUserId,
                EventType = "USER_UPDATED",
                Resource = "User",
                Details = $"Usuario {request.UserId} actualizado",
                IpAddress = request.IpAddress ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _auditRepository.InsertAsync(auditLog, cancellationToken);

            _logger.LogInformation("User {UserId} updated by {RequestingUserId}", request.UserId, request.RequestingUserId);

            // Paso 6: Retornar éxito
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", request.UserId);
            return Result<bool>.Failure("INTERNAL_ERROR", "Error actualizando usuario");
        }
    }
}
