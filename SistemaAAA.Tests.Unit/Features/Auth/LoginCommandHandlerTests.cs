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
/// Unit tests for LoginCommandHandler.
/// </summary>
public class LoginCommandHandlerTests
{
    private readonly Mock<IAuthRepository> _mockAuthRepository;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<ILogger<LoginCommandHandler>> _mockLogger;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _mockAuthRepository = new Mock<IAuthRepository>();
        _mockJwtService = new Mock<IJwtService>();
        _mockLogger = new Mock<ILogger<LoginCommandHandler>>();

        _handler = new LoginCommandHandler(
            _mockAuthRepository.Object,
            _mockJwtService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsSuccessWithAuthResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "usuario@example.com";
        var password = "P@ssw0rdSegura";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, 12);

        var user = new User
        {
            Id = userId,
            Email = email,
            PasswordHash = passwordHash,
            IsActive = true,
            LockedUntil = null,
            FailedLoginAttempts = 0
        };

        var expectedAccessToken = "access_token_value";
        var expectedRoles = new List<string> { "User" };

        var loginCommand = new LoginCommand(email, password, "127.0.0.1");

        _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockAuthRepository.Setup(x => x.GetUserRolesAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(expectedRoles);
        _mockJwtService.Setup(x => x.GenerateAccessToken(userId, email, It.IsAny<string[]>())).Returns(expectedAccessToken);
        _mockJwtService.Setup(x => x.GenerateRefreshToken()).Returns("refresh_token_value");
        _mockAuthRepository.Setup(x => x.SaveRefreshTokenAsync(userId, "refresh_token_value", "127.0.0.1", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(loginCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.AccessToken.Should().Be(expectedAccessToken);
        result.Value!.RefreshToken.Should().Be("refresh_token_value");
        result.Value!.UserId.Should().Be(userId);
        result.Value!.Roles.Should().NotBeNull();

        _mockAuthRepository.Verify(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>()), Times.Once);
        _mockJwtService.Verify(x => x.GenerateAccessToken(userId, email, It.IsAny<string[]>()), Times.Once);
        _mockAuthRepository.Verify(x => x.SaveRefreshTokenAsync(userId, "refresh_token_value", "127.0.0.1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidPassword_ReturnsFailureWithGenericError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "usuario@example.com";
        var correctPassword = "P@ssw0rdSegura";
        var incorrectPassword = "WrongPassword";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(correctPassword, 12);

        var user = new User
        {
            Id = userId,
            Email = email,
            PasswordHash = passwordHash,
            IsActive = true,
            FailedLoginAttempts = 0,
            LockedUntil = null
        };

        var loginCommand = new LoginCommand(email, incorrectPassword, "127.0.0.1");

        _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockAuthRepository.Setup(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(loginCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
        _mockAuthRepository.Verify(x => x.UpdateAsync(It.Is<User>(u => u.FailedLoginAttempts == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentEmail_ReturnsFailureWithSameGenericError()
    {
        // Arrange
        var email = "noexiste@example.com";
        var loginCommand = new LoginCommand(email, "anypassword", "127.0.0.1");

        _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(loginCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
        _mockAuthRepository.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithLockedAccount_ReturnsFailureWithLockedError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "bloqueado@example.com";
        var password = "P@ssw0rdSegura";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, 12);

        var user = new User
        {
            Id = userId,
            Email = email,
            PasswordHash = passwordHash,
            IsActive = true,
            LockedUntil = DateTime.UtcNow.AddMinutes(10),
            FailedLoginAttempts = 5
        };

        var loginCommand = new LoginCommand(email, password, "127.0.0.1");

        _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(loginCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCOUNT_LOCKED");
        _mockAuthRepository.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AfterFifthFailedAttempt_BlocksAccount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "usuario@example.com";
        var incorrectPassword = "WrongPassword";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("correct", 12);

        var user = new User
        {
            Id = userId,
            Email = email,
            PasswordHash = passwordHash,
            IsActive = true,
            FailedLoginAttempts = 4,
            LockedUntil = null
        };

        var loginCommand = new LoginCommand(email, incorrectPassword, "127.0.0.1");

        _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockAuthRepository.Setup(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(loginCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
        _mockAuthRepository.Verify(x => x.UpdateAsync(
            It.Is<User>(u => u.FailedLoginAttempts == 5 && u.LockedUntil.HasValue),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInactiveAccount_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "inactivo@example.com";
        var password = "P@ssw0rdSegura";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, 12);

        var user = new User
        {
            Id = userId,
            Email = email,
            PasswordHash = passwordHash,
            IsActive = false,
            LockedUntil = null,
            FailedLoginAttempts = 0
        };

        var loginCommand = new LoginCommand(email, password, "127.0.0.1");

        _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(loginCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCOUNT_INACTIVE");
    }
}
