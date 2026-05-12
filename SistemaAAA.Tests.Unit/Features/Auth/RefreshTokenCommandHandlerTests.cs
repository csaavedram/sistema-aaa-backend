using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Application.Features.Auth;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Tests.Unit.Features.Auth;

/// <summary>
/// Unit tests for RefreshTokenCommandHandler.
/// </summary>
public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IAuthRepository> _mockAuthRepository;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<ILogger<RefreshTokenCommandHandler>> _mockLogger;
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _mockAuthRepository = new Mock<IAuthRepository>();
        _mockJwtService = new Mock<IJwtService>();
        _mockLogger = new Mock<ILogger<RefreshTokenCommandHandler>>();

        _handler = new RefreshTokenCommandHandler(
            _mockAuthRepository.Object,
            _mockJwtService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidToken_ReturnsNewAccessToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var refreshTokenValue = "valid_refresh_token_value";
        var newAccessToken = "new_access_token";
        var newRefreshToken = "new_refresh_token";

        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(refreshTokenValue, 12),
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var roles = new List<string> { "Admin" };

        var cmd = new RefreshTokenCommand { RefreshToken = refreshTokenValue, IpAddress = "127.0.0.1" };

        _mockAuthRepository.Setup(x => x.GetRefreshTokenByTokenAsync(refreshTokenValue, It.IsAny<CancellationToken>())).ReturnsAsync(storedToken);
        _mockAuthRepository.Setup(x => x.GetUserRolesAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(roles);
        _mockJwtService.Setup(x => x.GenerateAccessToken(userId, string.Empty, It.IsAny<string[]>())).Returns(newAccessToken);
        _mockJwtService.Setup(x => x.GenerateRefreshToken()).Returns(newRefreshToken);
        _mockAuthRepository.Setup(x => x.RevokeRefreshTokenAsync(storedToken.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockAuthRepository.Setup(x => x.SaveRefreshTokenAsync(userId, newRefreshToken, string.Empty, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be(newAccessToken);
        result.Value.RefreshToken.Should().Be(newRefreshToken);
        _mockAuthRepository.Verify(x => x.RevokeRefreshTokenAsync(storedToken.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockAuthRepository.Verify(x => x.SaveRefreshTokenAsync(userId, newRefreshToken, "127.0.0.1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExpiredToken_ReturnsError()
    {
        // Arrange
        var refreshTokenValue = "expired_token";
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = BCrypt.Net.BCrypt.HashPassword(refreshTokenValue, 12),
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var cmd = new RefreshTokenCommand { RefreshToken = refreshTokenValue, IpAddress = "127.0.0.1" };

        _mockAuthRepository.Setup(x => x.GetRefreshTokenByTokenAsync(refreshTokenValue, It.IsAny<CancellationToken>())).ReturnsAsync(storedToken);

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_EXPIRED");
        _mockAuthRepository.Verify(x => x.RevokeRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithRevokedToken_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var refreshTokenValue = "revoked_token";
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(refreshTokenValue, 12),
            IsRevoked = true,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var cmd = new RefreshTokenCommand { RefreshToken = refreshTokenValue, IpAddress = "127.0.0.1" };

        _mockAuthRepository.Setup(x => x.GetRefreshTokenByTokenAsync(refreshTokenValue, It.IsAny<CancellationToken>())).ReturnsAsync(storedToken);

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_REVOKED");
        _mockAuthRepository.Verify(x => x.RevokeRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidToken_ReturnsError()
    {
        // Arrange
        var refreshTokenValue = "invalid_token";
        var cmd = new RefreshTokenCommand { RefreshToken = refreshTokenValue, IpAddress = "127.0.0.1" };

        _mockAuthRepository.Setup(x => x.GetRefreshTokenByTokenAsync(refreshTokenValue, It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null);

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_INVALID");
    }

    [Fact]
    public async Task Handle_WithExpiredLock_AllowsRefresh()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var refreshTokenValue = "valid_token";
        var newAccessToken = "new_access_token";

        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(refreshTokenValue, 12),
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var roles = new List<string> { "User" };

        var cmd = new RefreshTokenCommand { RefreshToken = refreshTokenValue, IpAddress = "127.0.0.1" };

        _mockAuthRepository.Setup(x => x.GetRefreshTokenByTokenAsync(refreshTokenValue, It.IsAny<CancellationToken>())).ReturnsAsync(storedToken);
        _mockAuthRepository.Setup(x => x.GetUserRolesAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(roles);
        _mockJwtService.Setup(x => x.GenerateAccessToken(userId, string.Empty, It.IsAny<string[]>())).Returns(newAccessToken);
        _mockJwtService.Setup(x => x.GenerateRefreshToken()).Returns("new_refresh_token");
        _mockAuthRepository.Setup(x => x.RevokeRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockAuthRepository.Setup(x => x.SaveRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be(newAccessToken);
        result.Value.RefreshToken.Should().Be("new_refresh_token");
    }
}
