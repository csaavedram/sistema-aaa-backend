using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SistemaAAA.Application.Features.Auth;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;
using Xunit;

namespace SistemaAAA.Tests.Unit;

public class RefreshTokenCommandHandlerAppTests
{
    private readonly Mock<IAuthRepository> _mockAuthRepository;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<ILogger<RefreshTokenCommandHandler>> _mockLogger;
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerAppTests()
    {
        _mockAuthRepository = new Mock<IAuthRepository>();
        _mockJwtService = new Mock<IJwtService>();
        _mockLogger = new Mock<ILogger<RefreshTokenCommandHandler>>();
        _handler = new RefreshTokenCommandHandler(_mockAuthRepository.Object, _mockJwtService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewAccessToken()
    {
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var oldToken = new RefreshToken
        {
            Id = tokenId,
            UserId = userId,
            TokenHash = "old",
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        var cmd = new RefreshTokenCommand { RefreshToken = "old", IpAddress = "127.0.0.1" };

        _mockAuthRepository.Setup(x => x.GetRefreshTokenByTokenAsync("old", It.IsAny<CancellationToken>())).ReturnsAsync(oldToken);
        _mockAuthRepository.Setup(x => x.GetUserRolesAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<string> { "User" });
        _mockJwtService.Setup(x => x.GenerateAccessToken(userId, string.Empty, It.IsAny<string[]>())).Returns("newAccess");
        _mockJwtService.Setup(x => x.GenerateRefreshToken()).Returns("newRefresh");
        _mockAuthRepository.Setup(x => x.SaveRefreshTokenAsync(userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockAuthRepository.Setup(x => x.RevokeRefreshTokenAsync(tokenId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be("newAccess");
        result.Value.RefreshToken.Should().Be("newRefresh");
        _mockAuthRepository.Verify(x => x.RevokeRefreshTokenAsync(tokenId, It.IsAny<CancellationToken>()), Times.Once);
        _mockAuthRepository.Verify(x => x.SaveRefreshTokenAsync(userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshToken_WithExpiredToken_ReturnsError()
    {
        var tokenId = Guid.NewGuid();
        var expiredToken = new RefreshToken
        {
            Id = tokenId,
            UserId = Guid.NewGuid(),
            TokenHash = "old",
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var cmd = new RefreshTokenCommand { RefreshToken = "old", IpAddress = "127.0.0.1" };

        _mockAuthRepository.Setup(x => x.GetRefreshTokenByTokenAsync("old", It.IsAny<CancellationToken>())).ReturnsAsync(expiredToken);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_EXPIRED");
        _mockAuthRepository.Verify(x => x.RevokeRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshToken_WithRevokedToken_ReturnsError()
    {
        var tokenId = Guid.NewGuid();
        var revokedToken = new RefreshToken
        {
            Id = tokenId,
            UserId = Guid.NewGuid(),
            TokenHash = "old",
            IsRevoked = true,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        var cmd = new RefreshTokenCommand { RefreshToken = "old", IpAddress = "127.0.0.1" };

        _mockAuthRepository.Setup(x => x.GetRefreshTokenByTokenAsync("old", It.IsAny<CancellationToken>())).ReturnsAsync(revokedToken);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_REVOKED");
    }

    [Fact]
    public async Task RefreshToken_AfterSuccessfulRefresh_OldTokenIsRevoked()
    {
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var oldToken = new RefreshToken
        {
            Id = tokenId,
            UserId = userId,
            TokenHash = "old",
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        var cmd = new RefreshTokenCommand { RefreshToken = "old", IpAddress = "127.0.0.1" };

        _mockAuthRepository.Setup(x => x.GetRefreshTokenByTokenAsync("old", It.IsAny<CancellationToken>())).ReturnsAsync(oldToken);
        _mockAuthRepository.Setup(x => x.GetUserRolesAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<string> { "User" });
        _mockJwtService.Setup(x => x.GenerateAccessToken(userId, string.Empty, It.IsAny<string[]>())).Returns("newAccess");
        _mockJwtService.Setup(x => x.GenerateRefreshToken()).Returns("newRefresh");
        _mockAuthRepository.Setup(x => x.SaveRefreshTokenAsync(userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockAuthRepository.Setup(x => x.RevokeRefreshTokenAsync(tokenId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be("newAccess");
        result.Value.RefreshToken.Should().Be("newRefresh");
        _mockAuthRepository.Verify(x => x.RevokeRefreshTokenAsync(tokenId, It.IsAny<CancellationToken>()), Times.Once);
        _mockAuthRepository.Verify(x => x.SaveRefreshTokenAsync(userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshToken_WithTokenFromDifferentUser_ReturnsError()
    {
        var tokenId = Guid.NewGuid();
        var token = new RefreshToken
        {
            Id = tokenId,
            UserId = Guid.NewGuid(),
            TokenHash = "old",
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        var cmd = new RefreshTokenCommand { RefreshToken = "old", ExpectedUserId = Guid.NewGuid(), IpAddress = "127.0.0.1" };

        _mockAuthRepository.Setup(x => x.GetRefreshTokenByTokenAsync("old", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_INVALID");
    }
}