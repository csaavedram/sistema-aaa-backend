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
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IAuthRepository authRepository,
        IJwtService jwtService,
        ILogger<LoginCommandHandler> logger)
    {
        _authRepository = authRepository;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _authRepository.GetUserByEmailAsync(request.Email, cancellationToken);

            // If user doesn't exist, record audit with null UserId and return generic invalid credentials
            if (user is null)
            {
                _logger.LogWarning("Login failed for unknown user (email masked)");
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

                _logger.LogWarning("Invalid credentials for user: {UserId}", user.Id);
                return Result<AuthResponse>.Failure("AUTH_INVALID_CREDENTIALS", "Invalid credentials.");
            }

            // Successful login: reset counters, persist
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
            await _authRepository.UpdateAsync(user, cancellationToken);

            // Load roles and generate tokens
             var roles = await _authRepository.GetUserRolesAsync(user.Id, cancellationToken);

            var accessToken = _jwtService.GenerateAccessToken(user.Id, roles);
            var refreshToken = _jwtService.GenerateRefreshToken(user.Id);

            var expiresIn = 60; // minutes default
            var authResponse = new AuthResponse(accessToken, expiresIn * 60, user.Id, roles.ToArray());

            _logger.LogInformation("User logged in: {UserId}", user.Id);
            return Result<AuthResponse>.Success(authResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            return Result<AuthResponse>.Failure("INTERNAL_ERROR", "An unexpected error occurred.");
        }
    }
}
