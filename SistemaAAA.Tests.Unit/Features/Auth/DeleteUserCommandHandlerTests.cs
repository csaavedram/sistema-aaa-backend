using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SistemaAAA.Application.Common;
using SistemaAAA.Application.Features.Users;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Tests.Unit.Features.Users;

/// <summary>
/// Tests unitarios para DeleteUserCommandHandler.
/// Verifica soft delete, regla del último admin y auditoría.
/// </summary>
public class DeleteUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IAuthRepository> _mockAuthRepository;
    private readonly Mock<IAuditRepository> _mockAuditRepository;
    private readonly Mock<ILogger<DeleteUserCommandHandler>> _mockLogger;
    private readonly DeleteUserCommandHandler _handler;

    public DeleteUserCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockAuthRepository = new Mock<IAuthRepository>();
        _mockAuditRepository = new Mock<IAuditRepository>();
        _mockLogger = new Mock<ILogger<DeleteUserCommandHandler>>();

        _handler = new DeleteUserCommandHandler(
            _mockUserRepository.Object,
            _mockAuthRepository.Object,
            _mockAuditRepository.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidAdmin_WhenMultipleAdminsExist_DeactivatesUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();

        var user = new User { Id = userId, Email = "admin@example.com", IsActive = true };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockAuthRepository.Setup(x => x.GetUserRolesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Admin" });
        _mockUserRepository.Setup(x => x.GetAdminCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _mockUserRepository.Setup(x => x.DeleteAsync(userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAuditRepository.Setup(x => x.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = new DeleteUserCommand(userId, requestingUserId, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUserRepository.Verify(x => x.DeleteAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockAuditRepository.Verify(x => x.InsertAsync(
            It.Is<AuditLog>(l => l.EventType == "USER_DEACTIVATED"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ReturnsUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var cmd = new DeleteUserCommand(userId, requestingUserId, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
        _mockUserRepository.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DeleteLastAdmin_ReturnsCannotDeleteLastAdmin()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "lastadmin@example.com", IsActive = true };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockAuthRepository.Setup(x => x.GetUserRolesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Admin" });
        _mockUserRepository.Setup(x => x.GetAdminCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var cmd = new DeleteUserCommand(userId, requestingUserId, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CANNOT_DELETE_LAST_ADMIN");
        _mockUserRepository.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DeleteNonAdmin_SucceedsRegardlessOfAdminCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "user@example.com", IsActive = true };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockAuthRepository.Setup(x => x.GetUserRolesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "User" });
        _mockUserRepository.Setup(x => x.DeleteAsync(userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAuditRepository.Setup(x => x.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = new DeleteUserCommand(userId, requestingUserId, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUserRepository.Verify(x => x.DeleteAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockUserRepository.Verify(x => x.GetAdminCountAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockAuditRepository.Verify(x => x.InsertAsync(
            It.Is<AuditLog>(l => l.EventType == "USER_DEACTIVATED"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
