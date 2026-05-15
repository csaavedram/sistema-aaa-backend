using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SistemaAAA.Application.Features.Auth;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Tests.Unit.Features.Auth;

/// <summary>
/// Tests unitarios para ForgotPasswordCommandHandler.
/// Verifica la generación de tokens, envío de correos y protecciones de seguridad (anti-enumeración y anti-spam).
/// </summary>
public class ForgotPasswordCommandHandlerTests
{
    private readonly Mock<IAuthRepository> _mockAuthRepository;
    private readonly Mock<IPasswordResetTokenRepository> _mockTokenRepository;
    private readonly Mock<IAuditRepository> _mockAuditRepository;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<ForgotPasswordCommandHandler>> _mockLogger;
    private readonly ForgotPasswordCommandHandler _handler;

    public ForgotPasswordCommandHandlerTests()
    {
        _mockAuthRepository = new Mock<IAuthRepository>();
        _mockTokenRepository = new Mock<IPasswordResetTokenRepository>();
        _mockAuditRepository = new Mock<IAuditRepository>();
        _mockEmailService = new Mock<IEmailService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<ForgotPasswordCommandHandler>>();

        _mockConfiguration.Setup(x => x["App:FrontendUrl"]).Returns("https://localhost:4200");

        _handler = new ForgotPasswordCommandHandler(
            _mockAuthRepository.Object,
            _mockTokenRepository.Object,
            _mockAuditRepository.Object,
            _mockEmailService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithExistingEmail_SendsEmailAndReturnsSuccess()
    {
        // Arrange
        var email = "user@example.com";
        var user = new User { Id = Guid.NewGuid(), Email = email, IsActive = true };

        _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenRepository.Setup(x => x.HasActiveTokenAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockTokenRepository.Setup(x => x.CreateAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockEmailService.Setup(x => x.SendPasswordResetEmailAsync(email, It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockAuditRepository.Setup(x => x.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var cmd = new ForgotPasswordCommand(email, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockTokenRepository.Verify(x => x.CreateAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEmailService.Verify(x => x.SendPasswordResetEmailAsync(email, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentEmail_ReturnsSuccessWithoutSendingEmail()
    {
        // Arrange
        var email = "noexiste@example.com";
        _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var cmd = new ForgotPasswordCommand(email, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        // CRÍTICO: anti-enumeración — no revelar si el email existe
        result.IsSuccess.Should().BeTrue(); 
        _mockTokenRepository.Verify(x => x.CreateAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockEmailService.Verify(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInactiveUser_ReturnsSuccessWithoutSendingEmail()
    {
        // Arrange
        var email = "inactive@example.com";
        var user = new User { Id = Guid.NewGuid(), Email = email, IsActive = false };

        _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var cmd = new ForgotPasswordCommand(email, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockEmailService.Verify(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithExistingActiveToken_ReturnsSuccessWithoutSendingEmail()
    {
        // Arrange
        var email = "user@example.com";
        var user = new User { Id = Guid.NewGuid(), Email = email, IsActive = true };

        _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        
        // Simulamos que el usuario ya pidió un token recientemente
        _mockTokenRepository.Setup(x => x.HasActiveTokenAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var cmd = new ForgotPasswordCommand(email, "127.0.0.1");

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        // CRÍTICO: anti-spam — evitar envío masivo de correos
        result.IsSuccess.Should().BeTrue();
        _mockEmailService.Verify(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTokenRepository.Verify(x => x.CreateAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}