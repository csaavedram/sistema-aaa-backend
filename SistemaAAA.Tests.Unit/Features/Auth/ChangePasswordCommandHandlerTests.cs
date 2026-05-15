using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SistemaAAA.Application.Features.Auth;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Tests.Unit.Features.Auth;

/// <summary>
/// Tests unitarios para ChangePasswordCommandHandler.
/// Verifica la actualización de credenciales, fortaleza de contraseña y revocación global de sesiones.
/// </summary>
public class ChangePasswordCommandHandlerTests
{
    private readonly Mock<IAuthRepository> _mockAuthRepository;
    private readonly Mock<IAuditRepository> _mockAuditRepository;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly Mock<ILogger<ChangePasswordCommandHandler>> _mockLogger;
    private readonly ChangePasswordCommandHandler _handler;

    public ChangePasswordCommandHandlerTests()
    {
        _mockAuthRepository = new Mock<IAuthRepository>();
        _mockAuditRepository = new Mock<IAuditRepository>();
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _mockLogger = new Mock<ILogger<ChangePasswordCommandHandler>>();

        _handler = new ChangePasswordCommandHandler(
            _mockAuthRepository.Object,
            _mockAuditRepository.Object,
            _mockPasswordHasher.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidCurrentPassword_ChangesPasswordSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentPassword = "OldPassword123!";
        var newPassword = "NewStrongP@ss1!";
        var oldHash = "hash_old";
        var newHash = "hash_new";

        var user = new User { Id = userId, PasswordHash = oldHash };

        _mockAuthRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockPasswordHasher.Setup(x => x.Verify(currentPassword, oldHash)).Returns(true);
        _mockPasswordHasher.Setup(x => x.Verify(newPassword, oldHash)).Returns(false); // No son iguales
        _mockPasswordHasher.Setup(x => x.Hash(newPassword)).Returns(newHash);
        _mockAuthRepository.Setup(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockAuthRepository.Setup(x => x.RevokeAllUserRefreshTokensAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockAuditRepository.Setup(x => x.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var cmd = new ChangePasswordCommand(userId, currentPassword, newPassword, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockPasswordHasher.Verify(x => x.Hash(newPassword), Times.Once);
        _mockAuthRepository.Verify(x => x.UpdateAsync(It.Is<User>(u => u.PasswordHash == newHash), It.IsAny<CancellationToken>()), Times.Once);
        _mockAuthRepository.Verify(x => x.RevokeAllUserRefreshTokensAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithWrongCurrentPassword_ReturnsCurrentPasswordInvalid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentPassword = "WrongPassword!";
        var newPassword = "NewStrongP@ss1!";
        var user = new User { Id = userId, PasswordHash = "actual_hash" };

        _mockAuthRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockPasswordHasher.Setup(x => x.Verify(currentPassword, user.PasswordHash)).Returns(false); // Falla verificación

        var cmd = new ChangePasswordCommand(userId, currentPassword, newPassword, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CURRENT_PASSWORD_INVALID");
        _mockAuthRepository.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithSamePassword_ReturnsSamePassword()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentPassword = "SamePassword1!";
        var newPassword = "SamePassword1!";
        var user = new User { Id = userId, PasswordHash = "hash" };

        _mockAuthRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        
        // Verifica la actual y luego detecta que la nueva es idéntica
        _mockPasswordHasher.Setup(x => x.Verify(currentPassword, user.PasswordHash)).Returns(true);
        _mockPasswordHasher.Setup(x => x.Verify(newPassword, user.PasswordHash)).Returns(true);

        var cmd = new ChangePasswordCommand(userId, currentPassword, newPassword, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SAME_PASSWORD");
        _mockAuthRepository.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OnPasswordChange_RevokesAllRefreshTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentPassword = "OldPassword123!";
        var newPassword = "NewStrongP@ss1!";
        var user = new User { Id = userId, PasswordHash = "old_hash" };

        _mockAuthRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockPasswordHasher.Setup(x => x.Verify(currentPassword, user.PasswordHash)).Returns(true);
        _mockPasswordHasher.Setup(x => x.Verify(newPassword, user.PasswordHash)).Returns(false);
        _mockPasswordHasher.Setup(x => x.Hash(newPassword)).Returns("new_hash");

        var cmd = new ChangePasswordCommand(userId, currentPassword, newPassword, "127.0.0.1");

        // Act
        await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        // CRÍTICO: al cambiar contraseña se invalidan todos los dispositivos
        _mockAuthRepository.Verify(x => x.RevokeAllUserRefreshTokensAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}