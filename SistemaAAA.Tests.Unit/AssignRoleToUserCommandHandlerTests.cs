using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SistemaAAA.Application.Common;
using SistemaAAA.Application.Features.Roles;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Tests.Unit.Features.Roles;

/// <summary>
/// Tests unitarios para AssignRoleToUserCommandHandler.
/// Verifica asignación de roles, validaciones y auditoría.
/// </summary>
public class AssignRoleToUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IRoleRepository> _mockRoleRepository;
    private readonly Mock<IAuditRepository> _mockAuditRepository;
    private readonly Mock<ILogger<AssignRoleToUserCommandHandler>> _mockLogger;
    private readonly AssignRoleToUserCommandHandler _handler;

    public AssignRoleToUserCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockRoleRepository = new Mock<IRoleRepository>();
        _mockAuditRepository = new Mock<IAuditRepository>();
        _mockLogger = new Mock<ILogger<AssignRoleToUserCommandHandler>>();

        _handler = new AssignRoleToUserCommandHandler(
            _mockUserRepository.Object,
            _mockRoleRepository.Object,
            _mockAuditRepository.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidData_AssignsRoleSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var assignedByUserId = Guid.NewGuid();

        var user = new User { Id = userId, Email = "user@example.com", IsActive = true };
        var role = new Role { Id = roleId, Name = "Editor", IsSystem = false };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _mockRoleRepository.Setup(x => x.IsAssignedToUserAsync(roleId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockRoleRepository.Setup(x => x.AssignToUserAsync(roleId, userId, assignedByUserId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAuditRepository.Setup(x => x.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = new AssignRoleToUserCommand(userId, roleId, assignedByUserId, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockRoleRepository.Verify(x => x.AssignToUserAsync(roleId, userId, assignedByUserId, It.IsAny<CancellationToken>()), Times.Once);
        _mockAuditRepository.Verify(x => x.InsertAsync(
            It.Is<AuditLog>(l => l.EventType == "ROLE_ASSIGNED_TO_USER"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ReturnsUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var cmd = new AssignRoleToUserCommand(userId, roleId, Guid.NewGuid(), "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
        _mockRoleRepository.Verify(x => x.AssignToUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNonExistentRole_ReturnsRoleNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "user@example.com", IsActive = true };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        var cmd = new AssignRoleToUserCommand(userId, roleId, Guid.NewGuid(), "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ROLE_NOT_FOUND");
        _mockRoleRepository.Verify(x => x.AssignToUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAlreadyAssignedRole_ReturnsRoleAlreadyAssigned()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "user@example.com", IsActive = true };
        var role = new Role { Id = roleId, Name = "Admin", IsSystem = true };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockRoleRepository.Setup(x => x.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _mockRoleRepository.Setup(x => x.IsAssignedToUserAsync(roleId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cmd = new AssignRoleToUserCommand(userId, roleId, Guid.NewGuid(), "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ROLE_ALREADY_ASSIGNED");
        _mockRoleRepository.Verify(x => x.AssignToUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
