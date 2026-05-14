using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Maneja el cambio de contraseña del usuario autenticado.
/// </summary>
public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result<bool>>
{
    private readonly IAuthRepository _authRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del handler.
    /// </summary>
    /// <param name="authRepository">Repositorio de autenticación.</param>
    /// <param name="auditRepository">Repositorio de auditoría.</param>
    /// <param name="passwordHasher">Servicio de hash de contraseñas.</param>
    /// <param name="logger">Logger.</param>
    public ChangePasswordCommandHandler(
        IAuthRepository authRepository,
        IAuditRepository auditRepository,
        IPasswordHasher passwordHasher,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _authRepository = authRepository;
        _auditRepository = auditRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el cambio de contraseña, invalida refresh tokens y registra auditoría.
    /// </summary>
    /// <param name="request">Datos de la solicitud.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado de la operación.</returns>
    public async Task<Result<bool>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _authRepository.GetByIdAsync(request.UserId, cancellationToken);

            if (user is null)
            {
                _logger.LogWarning("Password change requested for missing user: {UserId}", request.UserId);
                return Result<bool>.Failure("USER_NOT_FOUND", "Usuario no encontrado");
            }

            if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            {
                _logger.LogWarning("Invalid current password for user: {UserId}", user.Id);
                return Result<bool>.Failure("CURRENT_PASSWORD_INVALID", "La contraseña actual es incorrecta");
            }

            if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
            {
                _logger.LogWarning("New password matches current password for user: {UserId}", user.Id);
                return Result<bool>.Failure("SAME_PASSWORD", "La nueva contraseña debe ser diferente a la actual");
            }

            if (!IsStrongPassword(request.NewPassword))
            {
                _logger.LogWarning("Weak new password provided for user: {UserId}", user.Id);
                return Result<bool>.Failure("WEAK_PASSWORD", "La contraseña no cumple los requisitos mínimos");
            }

            user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            user.FailedLoginAttempts = 0;

            await _authRepository.UpdateAsync(user, cancellationToken);
            await _authRepository.RevokeAllUserRefreshTokensAsync(user.Id, cancellationToken);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                EventType = "PASSWORD_CHANGED",
                Resource = "Auth",
                Details = "Contraseña cambiada por el usuario",
                IpAddress = request.IpAddress ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _auditRepository.InsertAsync(auditLog, cancellationToken);

            _logger.LogInformation("Password changed for user {UserId}", user.Id);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing password change for user: {UserId}", request.UserId);
            return Result<bool>.Failure("INTERNAL_ERROR", "Error procesando solicitud");
        }
    }

    /// <summary>
    /// Determina si una contraseña cumple la política mínima del sistema.
    /// </summary>
    /// <param name="password">Contraseña a validar.</param>
    /// <returns>True si cumple la política; en caso contrario, false.</returns>
    private static bool IsStrongPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return false;
        }

        var hasUppercase = false;
        var hasDigit = false;
        var hasSpecialCharacter = false;

        foreach (var character in password)
        {
            if (char.IsUpper(character))
            {
                hasUppercase = true;
            }

            if (char.IsDigit(character))
            {
                hasDigit = true;
            }

            if (!hasSpecialCharacter && !char.IsLetterOrDigit(character))
            {
                hasSpecialCharacter = true;
            }

            if (hasUppercase && hasDigit && hasSpecialCharacter)
            {
                return true;
            }
        }

        return false;
    }
}