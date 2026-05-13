using System.Security.Claims;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Handler para el comando de logout.
/// </summary>
public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result<bool>>
{
    private readonly IAuthRepository _authRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly IJwtService _jwtService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(
        IAuthRepository authRepository,
        IAuditRepository auditRepository,
        IJwtService jwtService,
        IMemoryCache cache,
        ILogger<LogoutCommandHandler> logger)
    {
        _authRepository = authRepository;
        _auditRepository = auditRepository;
        _jwtService = jwtService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Paso 1: revocar access token en caché
            if (!string.IsNullOrWhiteSpace(request.AccessToken))
            {
                try
                {
                    var jti = _jwtService.ExtractTokenId(request.AccessToken);
                    if (!string.IsNullOrWhiteSpace(jti))
                    {
                        // No loggear jti ni token
                        _cache.Set($"revoked:{jti}", true, new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                        });
                    }
                    else
                    {
                        _logger.LogWarning("Could not extract token id from access token for user {UserId}", request.UserId);
                    }
                }
                catch
                {
                    _logger.LogWarning("Could not extract token id from access token for user {UserId}", request.UserId);
                }
            }

            // Paso 2: revocar refresh token en BD
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                var stored = await _authRepository.GetRefreshTokenByTokenAsync(request.RefreshToken, cancellationToken);
                if (stored is null || stored.IsRevoked)
                {
                    _logger.LogWarning("Refresh token not found or already revoked for user {UserId}", request.UserId);
                }
                else
                {
                    await _authRepository.RevokeRefreshTokenAsync(stored.Id, cancellationToken);
                }
            }

            // Paso 3: auditar logout
            var log = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                EventType = "LOGOUT",
                Resource = "Auth",
                Details = "Logout exitoso",
                IpAddress = request.IpAddress ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _auditRepository.InsertAsync(log, cancellationToken);

            _logger.LogInformation("User {UserId} logged out from {IpAddress}", request.UserId, request.IpAddress);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during logout for user {UserId}", request.UserId);
            return Result<bool>.Failure("INTERNAL_ERROR", "Error procesando logout");
        }
    }
}
