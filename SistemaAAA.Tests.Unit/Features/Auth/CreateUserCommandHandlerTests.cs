using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Application.Features.Users;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Tests.Unit.Features.Users;

/// <summary>
/// Unit tests for CreateUserCommandHandler.
/// </summary>
public class CreateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IAuditRepository> _mockAuditRepository;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly Mock<ILogger<CreateUserCommandHandler>> _mockLogger;
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockAuditRepository = new Mock<IAuditRepository>();
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _mockLogger = new Mock<ILogger<CreateUserCommandHandler>>();

        _handler = new CreateUserCommandHandler(
            _mockUserRepository.Object,
            _mockAuditRepository.Object,
            _mockPasswordHasher.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidData_ReturnsSuccessWithUserId()
    {
        // Arrange
        var email = "nuevo@example.com";
        var password = "StrongP@ss1";
        var createdByUserId = Guid.NewGuid();

        _mockUserRepository.Setup(x => x.ExistsWithEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockPasswordHasher.Setup(x => x.Hash(password)).Returns(BCrypt.Net.BCrypt.HashPassword(password, 12));
        _mockUserRepository.Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockAuditRepository.Setup(x => x.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var cmd = new CreateUserCommand(email, password, createdByUserId, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Email.Should().Be(email);
        result.Value!.UserId.Should().NotBeEmpty();
        _mockUserRepository.Verify(x => x.CreateAsync(It.Is<User>(u => u.Email == email), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ReturnsError()
    {
        // Arrange
        var email = "dup@example.com";
        var createdByUserId = Guid.NewGuid();

        _mockUserRepository.Setup(x => x.ExistsWithEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var cmd = new CreateUserCommand(email, "StrongP@ss1", createdByUserId, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("EMAIL_ALREADY_EXISTS");
        _mockUserRepository.Verify(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PasswordIsHashedBeforeStorage()
    {
        // Arrange
        var email = "secure@example.com";
        var password = "VeryS3cure!";
        var createdByUserId = Guid.NewGuid();
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password, 12);
        User? captured = null;

        _mockUserRepository.Setup(x => x.ExistsWithEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockPasswordHasher.Setup(x => x.Hash(password)).Returns(hashedPassword);
        _mockUserRepository.Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Callback<User, CancellationToken>((u, ct) => { captured = u; }).Returns(Task.CompletedTask);
        _mockAuditRepository.Setup(x => x.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var cmd = new CreateUserCommand(email, password, createdByUserId, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.PasswordHash.Should().Be(hashedPassword);
        _mockPasswordHasher.Verify(x => x.Hash(password), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyEmail_FailsValidation()
    {
        // Arrange
        var createdByUserId = Guid.NewGuid();

        var cmd = new CreateUserCommand("", "StrongP@ss1", createdByUserId, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_EMAIL");
    }

    [Fact]
    public async Task Handle_AuditsUserCreation()
    {
        // Arrange
        var email = "audited@example.com";
        var password = "StrongP@ss1";
        var createdByUserId = Guid.NewGuid();
        AuditLog? capturedAuditLog = null;

        _mockUserRepository.Setup(x => x.ExistsWithEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockPasswordHasher.Setup(x => x.Hash(password)).Returns(BCrypt.Net.BCrypt.HashPassword(password, 12));
        _mockUserRepository.Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockAuditRepository.Setup(x => x.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>())).Callback<AuditLog, CancellationToken>((log, ct) => { capturedAuditLog = log; }).Returns(Task.CompletedTask);

        var cmd = new CreateUserCommand(email, password, createdByUserId, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockAuditRepository.Verify(x => x.InsertAsync(It.Is<AuditLog>(log => log.EventType == "USER_CREATED"), It.IsAny<CancellationToken>()), Times.Once);
        capturedAuditLog.Should().NotBeNull();
        capturedAuditLog!.UserId.Should().Be(createdByUserId);
    }
}
