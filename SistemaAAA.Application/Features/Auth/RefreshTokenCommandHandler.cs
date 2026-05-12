using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Handler para rotar refresh tokens.
/// </summary>
public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly IAuthRepository _authRepository;
    private readonly IJwtService _jwtService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IAuthRepository authRepository,
        IJwtService jwtService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _authRepository = authRepository;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return Result<AuthResponse>.Failure("TOKEN_INVALID", "Refresh token is required.");
            }

            var storedToken = await _authRepository.GetRefreshTokenByTokenAsync(request.RefreshToken, cancellationToken);

            if (storedToken is null)
            {
                _logger.LogWarning("Refresh token not found");
                return Result<AuthResponse>.Failure("TOKEN_INVALID", "Refresh token is invalid.");
            }

            if (request.ExpectedUserId.HasValue && storedToken.UserId != request.ExpectedUserId.Value)
            {
                _logger.LogWarning("Refresh token user mismatch for userId: {UserId}", storedToken.UserId);
                return Result<AuthResponse>.Failure("TOKEN_INVALID", "Refresh token is invalid.");
            }

            if (storedToken.IsRevoked)
            {
                _logger.LogWarning("Refresh token already revoked for userId: {UserId}", storedToken.UserId);
                return Result<AuthResponse>.Failure("TOKEN_REVOKED", "Refresh token is revoked.");
            }

            if (storedToken.ExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh token expired for userId: {UserId}", storedToken.UserId);
                return Result<AuthResponse>.Failure("TOKEN_EXPIRED", "Refresh token is expired.");
            }

            var roles = await _authRepository.GetUserRolesAsync(storedToken.UserId, cancellationToken);
            var accessToken = _jwtService.GenerateAccessToken(storedToken.UserId, string.Empty, roles.ToArray());
            var newRefreshToken = _jwtService.GenerateRefreshToken();

            await _authRepository.RevokeRefreshTokenAsync(storedToken.Id, cancellationToken);
            await _authRepository.SaveRefreshTokenAsync(storedToken.UserId, newRefreshToken, request.IpAddress ?? string.Empty, cancellationToken);

            _logger.LogInformation("Refresh token rotated for userId: {UserId}", storedToken.UserId);
            var authResponse = new AuthResponse(accessToken, newRefreshToken, 60 * 60, storedToken.UserId, roles.ToArray());
            return Result<AuthResponse>.Success(authResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during refresh token rotation");
            return Result<AuthResponse>.Failure("INTERNAL_ERROR", "An unexpected error occurred.");
        }
    }
}