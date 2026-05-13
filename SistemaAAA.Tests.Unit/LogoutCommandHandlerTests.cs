using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using SistemaAAA.Application.Common;
using SistemaAAA.Application.Features.Auth;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Tests.Unit.Features.Auth;

/// <summary>
/// Tests unitarios para LogoutCommandHandler.
/// Verifica la revocación dual (caché + BD) y la auditoría del logout.
/// </summary>
public class LogoutCommandHandlerTests
{
    private readonly Mock<IAuthRepository> _mockAuthRepository;
    private readonly Mock<IAuditRepository> _mockAuditRepository;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<IMemoryCache> _mockCache;
    private readonly Mock<ILogger<LogoutCommandHandler>> _mockLogger;
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _mockAuthRepository = new Mock<IAuthRepository>();
        _mockAuditRepository = new Mock<IAuditRepository>();
        _mockJwtService = new Mock<IJwtService>();
        _mockCache = new Mock<IMemoryCache>();
        _mockLogger = new Mock<ILogger<LogoutCommandHandler>>();

        _handler = new LogoutCommandHandler(
            _mockAuthRepository.Object,
            _mockAuditRepository.Object,
            _mockJwtService.Object,
            _mockCache.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidTokens_RevokesAccessTokenInCacheAndRefreshTokenInDb()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var jti = Guid.NewGuid().ToString();
        var accessToken = "valid.access.token";
        var refreshTokenValue = "valid_refresh_token";

        var storedRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "hash",
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _mockJwtService.Setup(x => x.ExtractTokenId(accessToken)).Returns(jti);

        _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(Mock.Of<ICacheEntry>());

        _mockAuthRepository.Setup(x => x.GetRefreshTokenByTokenAsync(refreshTokenValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedRefreshToken);
        _mockAuthRepository.Setup(x => x.RevokeRefreshTokenAsync(storedRefreshToken.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAuditRepository.Setup(x => x.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = new LogoutCommand(userId, accessToken, refreshTokenValue, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockJwtService.Verify(x => x.ExtractTokenId(accessToken), Times.Once);
        _mockAuthRepository.Verify(x => x.RevokeRefreshTokenAsync(storedRefreshToken.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidAccessToken_ContinuesLogoutSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accessToken = "malformed.token";
        var refreshTokenValue = "valid_refresh_token";

        var storedRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "hash",
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _mockJwtService.Setup(x => x.ExtractTokenId(accessToken)).Returns((string?)null);
        _mockAuthRepository.Setup(x => x.GetRefreshTokenByTokenAsync(refreshTokenValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedRefreshToken);
        _mockAuthRepository.Setup(x => x.RevokeRefreshTokenAsync(storedRefreshToken.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAuditRepository.Setup(x => x.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = new LogoutCommand(userId, accessToken, refreshTokenValue, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockCache.Verify(x => x.CreateEntry(It.IsAny<object>()), Times.Never);
        _mockAuthRepository.Verify(x => x.RevokeRefreshTokenAsync(storedRefreshToken.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithRefreshTokenNotFound_ContinuesLogoutSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var jti = Guid.NewGuid().ToString();
        var accessToken = "valid.access.token";
        var refreshTokenValue = "not_found_token";

        _mockJwtService.Setup(x => x.ExtractTokenId(accessToken)).Returns(jti);
        _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(Mock.Of<ICacheEntry>());
        _mockAuthRepository.Setup(x => x.GetRefreshTokenByTokenAsync(refreshTokenValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);
        _mockAuditRepository.Setup(x => x.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = new LogoutCommand(userId, accessToken, refreshTokenValue, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockAuthRepository.Verify(x => x.RevokeRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Always_AuditsLogoutEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        AuditLog? capturedLog = null;

        _mockJwtService.Setup(x => x.ExtractTokenId(It.IsAny<string>())).Returns((string?)null);
        _mockAuthRepository.Setup(x => x.GetRefreshTokenByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);
        _mockAuditRepository.Setup(x => x.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLog, CancellationToken>((log, _) => capturedLog = log)
            .Returns(Task.CompletedTask);

        var cmd = new LogoutCommand(userId, "token", "refresh", "192.168.1.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        _mockAuditRepository.Verify(x => x.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
        capturedLog.Should().NotBeNull();
        capturedLog!.EventType.Should().Be("LOGOUT");
        capturedLog.UserId.Should().Be(userId);
    }
}
