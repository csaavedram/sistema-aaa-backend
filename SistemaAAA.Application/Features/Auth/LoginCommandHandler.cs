using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Handler para el comando de login.
/// </summary>
public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly IAuthRepository _authRepository;
    private readonly IJwtService _jwtService;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly ILogger<LoginCommandHandler> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del handler de login.
    /// </summary>
    /// <param name="authRepository">Repositorio de autenticación.</param>
    /// <param name="jwtService">Servicio JWT.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="auditRepository">Repositorio de auditoría opcional.</param>
    public LoginCommandHandler(
        IAuthRepository authRepository,
        IJwtService jwtService,
        IPermissionRepository permissionRepository,
        ILogger<LoginCommandHandler> logger,
        IAuditRepository? auditRepository = null)
    {
        _authRepository = authRepository;
        _jwtService = jwtService;
        _permissionRepository = permissionRepository;
        _logger = logger;
        _auditRepository = auditRepository;
    }

    /// <summary>
    /// Ejecuta el login validando credenciales, aplicando bloqueo y generando la respuesta de autenticación.
    /// </summary>
    /// <param name="request">Datos del login.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado con la respuesta de autenticación o un error de negocio.</returns>
    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _authRepository.GetUserByEmailAsync(request.Email, cancellationToken);

            if (user is null)
            {
                await RecordAuditAsync(null, "LOGIN_FAILURE", request.IpAddress, cancellationToken);
                return Result<AuthResponse>.Failure("AUTH_INVALID_CREDENTIALS", "Invalid credentials.");
            }

            // 1. Check locked
            if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            {
                _logger.LogWarning("Login attempt to locked account: {UserId}", user.Id);
                return Result<AuthResponse>.Failure("ACCOUNT_LOCKED", "Account is locked.");
            }

            // 2. Check inactive
            if (!user.IsActive)
            {
                _logger.LogWarning("Login attempt to inactive account: {UserId}", user.Id);
                return Result<AuthResponse>.Failure("ACCOUNT_INACTIVE", "Account is inactive.");
            }

            // 3. Verify credentials
            var passwordMatches = false;
            try
            {
                passwordMatches = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            }
            catch
            {
                passwordMatches = false;
            }

            if (!passwordMatches)
            {
                user.FailedLoginAttempts += 1;
                if (user.FailedLoginAttempts >= 5)
                {
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
                }

                await _authRepository.UpdateAsync(user, cancellationToken);

                await RecordAuditAsync(user.Id, "LOGIN_FAILURE", request.IpAddress, cancellationToken);

                _logger.LogWarning("Invalid credentials for user: {UserId}", user.Id);
                return Result<AuthResponse>.Failure("AUTH_INVALID_CREDENTIALS", "Invalid credentials.");
            }

            // Successful login: reset counters, persist
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
            await _authRepository.UpdateAsync(user, cancellationToken);

            // Load roles and generate tokens
            var roles = await _authRepository.GetUserRolesAsync(user.Id, cancellationToken);
            var permissions = await _permissionRepository.GetByUserIdAsync(user.Id, cancellationToken);
            var permissionNames = permissions.Select(p => p.Name).ToArray();

            var accessToken = _jwtService.GenerateAccessToken(user.Id, user.Email, roles.ToArray(), permissionNames);
            var refreshToken = _jwtService.GenerateRefreshToken();

            await _authRepository.SaveRefreshTokenAsync(user.Id, refreshToken, request.IpAddress, cancellationToken);

            await RecordAuditAsync(user.Id, "LOGIN_SUCCESS", request.IpAddress, cancellationToken);

            var expiresIn = 60; // minutes default
            var authResponse = new AuthResponse(accessToken, refreshToken, expiresIn * 60, user.Id, roles.ToArray());

            _logger.LogInformation("User logged in: {UserId}", user.Id);
            return Result<AuthResponse>.Success(authResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            return Result<AuthResponse>.Failure("INTERNAL_ERROR", "An unexpected error occurred.");
        }
    }

    private async Task RecordAuditAsync(Guid? userId, string eventType, string ipAddress, CancellationToken cancellationToken)
    {
        if (_auditRepository is null)
        {
            return;
        }

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = eventType,
            Resource = "Auth",
            Details = eventType == "LOGIN_SUCCESS" ? "Login success" : "Login failure",
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        await _auditRepository.InsertAsync(auditLog, cancellationToken);
    }
}
