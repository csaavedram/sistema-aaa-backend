using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SistemaAAA.Application.Common;
using SistemaAAA.Application.Features.Auth;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Tests.Unit.Features.Auth;

/// <summary>
/// Tests unitarios para ResetPasswordCommandHandler.
/// Verifica validación de token, contraseña y actualización de usuario.
/// </summary>
public class ResetPasswordCommandHandlerTests
{
    private readonly Mock<IPasswordResetTokenRepository> _mockTokenRepository;
    private readonly Mock<IAuthRepository> _mockAuthRepository;
    private readonly Mock<IAuditRepository> _mockAuditRepository;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly Mock<ILogger<ResetPasswordCommandHandler>> _mockLogger;
    private readonly ResetPasswordCommandHandler _handler;

    public ResetPasswordCommandHandlerTests()
    {
        _mockTokenRepository = new Mock<IPasswordResetTokenRepository>();
        _mockAuthRepository = new Mock<IAuthRepository>();
        _mockAuditRepository = new Mock<IAuditRepository>();
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _mockLogger = new Mock<ILogger<ResetPasswordCommandHandler>>();

        _handler = new ResetPasswordCommandHandler(
            _mockTokenRepository.Object,
            _mockAuthRepository.Object,
            _mockAuditRepository.Object,
            _mockPasswordHasher.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidToken_ResetsPasswordSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var tokenPlano = "valid-reset-token-with-enough-length";
        var newPassword = "NewP@ssw0rd!";
        var hashedPassword = "$2a$12$hashedpassword";

        var storedToken = new PasswordResetToken
        {
            Id = tokenId,
            UserId = userId,
            TokenHash = "computed_hash",
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var user = new User
        {
            Id = userId,
            Email = "user@example.com",
            PasswordHash = "old_hash",
            IsActive = true
        };

        _mockTokenRepository.Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);
        _mockAuthRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockPasswordHasher.Setup(x => x.Hash(newPassword)).Returns(hashedPassword);
        _mockAuthRepository.Setup(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTokenRepository.Setup(x => x.MarkAsUsedAsync(tokenId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAuditRepository.Setup(x => x.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = new ResetPasswordCommand(tokenPlano, newPassword, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockPasswordHasher.Verify(x => x.Hash(newPassword), Times.Once);
        _mockAuthRepository.Verify(x => x.UpdateAsync(It.Is<User>(u => u.PasswordHash == hashedPassword), It.IsAny<CancellationToken>()), Times.Once);
        _mockTokenRepository.Verify(x => x.MarkAsUsedAsync(tokenId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidToken_ReturnsTokenInvalid()
    {
        // Arrange
        _mockTokenRepository.Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordResetToken?)null);

        var cmd = new ResetPasswordCommand("invalid-token-value-x", "NewP@ssw0rd!", "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_INVALID");
        _mockAuthRepository.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithExpiredToken_ReturnsTokenExpired()
    {
        // Arrange
        var storedToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "hash",
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddMinutes(-65)
        };

        _mockTokenRepository.Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        var cmd = new ResetPasswordCommand("some-token-value-here", "NewP@ssw0rd!", "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_EXPIRED");
        _mockAuthRepository.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAlreadyUsedToken_ReturnsTokenAlreadyUsed()
    {
        // Arrange
        var storedToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "hash",
            IsUsed = true,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        _mockTokenRepository.Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        var cmd = new ResetPasswordCommand("some-token-value-here", "NewP@ssw0rd!", "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_ALREADY_USED");
        _mockAuthRepository.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithWeakPassword_ReturnsWeakPassword_AndNeverUpdatesUser()
    {
        // Arrange
        var storedToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "hash",
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        _mockTokenRepository.Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        var cmd = new ResetPasswordCommand("some-token-value-here", "weak", "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("WEAK_PASSWORD");
        _mockAuthRepository.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTokenRepository.Verify(x => x.MarkAsUsedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
