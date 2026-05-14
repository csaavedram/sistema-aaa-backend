using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Maneja el comando de reseteo de contraseña. Valida el token,
/// verifica la nueva contraseña y actualiza las credenciales del usuario.
/// </summary>
public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result<bool>>
{
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IAuthRepository _authRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IPasswordResetTokenRepository tokenRepository,
        IAuthRepository authRepository,
        IAuditRepository auditRepository,
        IPasswordHasher passwordHasher,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _tokenRepository = tokenRepository;
        _authRepository = authRepository;
        _auditRepository = auditRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    /// <summary>Procesa el reseteo de contraseña con el token proporcionado.</summary>
    /// <param name="request">Comando con el token y la nueva contraseña.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    public async Task<Result<bool>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return Result<bool>.Failure("TOKEN_INVALID", "Token inválido");
            }

            var tokenHash = HashToken(request.Token);
            var storedToken = await _tokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

            if (storedToken is null)
            {
                return Result<bool>.Failure("TOKEN_INVALID", "Token inválido");
            }

            if (storedToken.IsUsed)
            {
                return Result<bool>.Failure("TOKEN_ALREADY_USED", "Token ya utilizado");
            }

            if (storedToken.ExpiresAt < DateTime.UtcNow)
            {
                return Result<bool>.Failure("TOKEN_EXPIRED", "Token expirado");
            }

            if (!IsStrongPassword(request.NewPassword))
            {
                return Result<bool>.Failure("WEAK_PASSWORD", "La contraseña no cumple los requisitos");
            }

            var user = await _authRepository.GetByIdAsync(storedToken.UserId, cancellationToken);
            if (user is null)
            {
                return Result<bool>.Failure("USER_NOT_FOUND", "Usuario no encontrado");
            }

            user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;

            await _authRepository.UpdateAsync(user, cancellationToken);
            await _tokenRepository.MarkAsUsedAsync(storedToken.Id, cancellationToken);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                EventType = "PASSWORD_RESET_COMPLETED",
                Resource = "Auth",
                Details = "Contraseña reseteada correctamente",
                IpAddress = request.IpAddress ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _auditRepository.InsertAsync(auditLog, cancellationToken);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando solicitud de reset de contraseña");
            return Result<bool>.Failure("INTERNAL_ERROR", "Error procesando solicitud");
        }
    }

    private static bool IsStrongPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return false;
        }

        var hasUppercase = false;
        var hasDigit = false;
        var specialCharacters = "!@#$%^&*()-_=+[{]}\\|;:'\",<.>/?`~";
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

            if (!hasSpecialCharacter && specialCharacters.Contains(character))
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

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}