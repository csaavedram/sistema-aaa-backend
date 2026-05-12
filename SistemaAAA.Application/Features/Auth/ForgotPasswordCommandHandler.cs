using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Handler que orquesta el flujo completo de solicitud de reseteo de contraseña.
///
/// Flujo:
/// 1. Buscar usuario por email (anti-enumeración: devolver Success incluso si no existe).
/// 2. Verificar si ya existe un token activo (anti-spam).
/// 3. Generar token único en claro (64 bytes aleatorios en Base64).
/// 4. Hashear el token con SHA256 para almacenamiento seguro.
/// 5. Persistir token hasheado en BD (válido por 1 hora, single-use).
/// 6. Construir enlace de reset con token plano (el usuario recibe el token plano en el correo).
/// 7. Enviar correo con enlace.
/// 8. Auditar evento como "PASSWORD_RESET_REQUESTED".
/// 9. Retornar éxito.
///
/// Seguridad crítica:
/// - NUNCA loggear el email, token plano, ni reset link.
/// - NUNCA diferenciar en la respuesta si el email existe o no (anti-enumeración).
/// - Token plano NUNCA se almacena en BD; solo su hash SHA256.
/// - Token es de un solo uso (IsUsed=true lo invalida incluso si no ha expirado).
/// - Token expira en 1 hora.
/// </summary>
public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result<bool>>
{
    private readonly IAuthRepository _authRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    /// <summary>
    /// Constructor con todas las dependencias necesarias.
    /// </summary>
    public ForgotPasswordCommandHandler(
        IAuthRepository authRepository,
        IPasswordResetTokenRepository tokenRepository,
        IAuditRepository auditRepository,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _authRepository = authRepository;
        _tokenRepository = tokenRepository;
        _auditRepository = auditRepository;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Orquesta el flujo completo de solicitud de reseteo de contraseña.
    /// </summary>
    public async Task<Result<bool>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Paso 1: Buscar usuario por email
            var user = await _authRepository.GetUserByEmailAsync(request.Email, cancellationToken);

            // Paso 2: Anti-enumeración (CRÍTICO)
            // Si el usuario no existe o está inactivo, devolver Success sin revelar
            if (user is null || !user.IsActive)
            {
                _logger.LogWarning("Password reset requested for unknown or inactive email");
                return Result<bool>.Success(true);
            }

            // Paso 3: Verificar si el usuario ya tiene un token activo (anti-spam)
            var hasActiveToken = await _tokenRepository.HasActiveTokenAsync(user.Id, cancellationToken);
            if (hasActiveToken)
            {
                _logger.LogWarning("Password reset already pending for user {UserId}", user.Id);
                return Result<bool>.Success(true);
            }

            // Paso 4: Generar token plano (64 bytes aleatorios)
            var tokenPlano = GenerateResetToken();

            // Paso 5: Hashear el token para almacenamiento seguro
            var tokenHash = HashToken(tokenPlano);

            // Paso 6: Persistir token hasheado en BD
            var passwordResetToken = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            await _tokenRepository.CreateAsync(passwordResetToken, cancellationToken);

            // Paso 7: Construir enlace de reset
            var frontendUrl = _configuration["App:FrontendUrl"]
                ?? throw new InvalidOperationException("Configuración 'App:FrontendUrl' no encontrada");

            var resetLink = $"{frontendUrl}/reset-password?token={Uri.EscapeDataString(tokenPlano)}";

            // Paso 8: Enviar correo
            await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink, cancellationToken);

            // Paso 9: Auditar evento
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                EventType = "PASSWORD_RESET_REQUESTED",
                Resource = "Auth",
                Details = "Token de reset de contraseña generado y enviado por correo",
                IpAddress = request.IpAddress,
                CreatedAt = DateTime.UtcNow
            };

            await _auditRepository.InsertAsync(auditLog, cancellationToken);

            // Paso 10: Retornar éxito
            _logger.LogInformation("Password reset token sent for user {UserId}", user.Id);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando solicitud de reset de contraseña");
            return Result<bool>.Failure("INTERNAL_ERROR", "Error procesando solicitud");
        }
    }

    /// <summary>
    /// Genera un token de reseteo único en texto claro (64 bytes aleatorios en Base64).
    /// </summary>
    /// <remarks>
    /// <para>
    /// El token plano se envía al usuario en el correo y se usa para validar
    /// la solicitud de reset en la capa API. NUNCA se almacena en BD; solo su hash.
    /// </para>
    /// <para>
    /// 64 bytes = 512 bits de entropía, suficiente para resistir ataques de fuerza bruta.
    /// </para>
    /// </remarks>
    /// <returns>Token en Base64 (seguro para transmitir por URL y correo).</returns>
    private string GenerateResetToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    /// <summary>
    /// Hashea el token con SHA256 para almacenamiento seguro en BD.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Solo el hash se persiste en BD. Si la BD se ve comprometida, los tokens planos
    /// no son recuperables (hash de una sola dirección).
    /// </para>
    /// <para>
    /// El hash se usa para validar tokens en ResetPasswordCommandHandler:
    /// comparar HashToken(tokenProvidedByUser) con PasswordResetToken.TokenHash en BD.
    /// </para>
    /// </remarks>
    /// <param name="token">Token en claro para hashear.</param>
    /// <returns>Hash SHA256 en Base64.</returns>
    private string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
