using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Features.Auth;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Tests.Unit
{
    public class LoginCommandHandlerAppTests
    {
        private readonly Mock<IAuthRepository> _mockAuthRepository;
        private readonly Mock<IJwtService> _mockJwtService;
        private readonly Mock<ILogger<LoginCommandHandler>> _mockLogger;
        private readonly LoginCommandHandler _handler;

        public LoginCommandHandlerAppTests()
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

            var loginCommand = new LoginCommand(email, password, "127.0.0.1");

            _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _mockAuthRepository.Setup(x => x.GetUserRolesAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<string>{"User"});
            _mockJwtService.Setup(x => x.GenerateAccessToken(userId, It.IsAny<List<string>>())).Returns(expectedAccessToken);
            _mockJwtService.Setup(x => x.GenerateRefreshToken(userId)).Returns("refresh_token_value");

            var result = await _handler.Handle(new LoginCommand(email, password, "127.0.0.1"), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.AccessToken.Should().Be(expectedAccessToken);
            result.Value.UserId.Should().Be(userId);

            _mockAuthRepository.Verify(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>()), Times.Once);
            _mockJwtService.Verify(x => x.GenerateAccessToken(userId, It.IsAny<List<string>>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithInvalidPassword_ReturnsFailureWithGenericError()
        {
            var userId = Guid.NewGuid();
            var email = "usuario@example.com";
            var correctPassword = "P@ssw0rdSegura";
            var incorrectPassword = "WrongPassword";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(correctPassword, 12);

            var user = new User { Id = userId, Email = email, PasswordHash = passwordHash, IsActive = true, FailedLoginAttempts = 0 };

            _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _mockAuthRepository.Setup(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await _handler.Handle(new LoginCommand(email, incorrectPassword, "127.0.0.1"), CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
            _mockAuthRepository.Verify(x => x.UpdateAsync(It.Is<User>(u => u.FailedLoginAttempts == 1), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithNonExistentEmail_ReturnsFailureWithSameGenericError()
        {
            var email = "noexiste@example.com";
            _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

            var result = await _handler.Handle(new LoginCommand(email, "any", "127.0.0.1"), CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
            _mockAuthRepository.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_WithLockedAccount_ReturnsFailureWithLockedError()
        {
            var userId = Guid.NewGuid();
            var email = "bloqueado@example.com";
            var password = "P@ssw0rdSegura";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, 12);

            var user = new User { Id = userId, Email = email, PasswordHash = passwordHash, IsActive = true, LockedUntil = DateTime.UtcNow.AddMinutes(10), FailedLoginAttempts = 5 };
            _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var result = await _handler.Handle(new LoginCommand(email, password, "127.0.0.1"), CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be("ACCOUNT_LOCKED");
            _mockAuthRepository.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_AfterFifthFailedAttempt_BlocksAccount()
        {
            var userId = Guid.NewGuid();
            var email = "usuario@example.com";
            var incorrectPassword = "WrongPassword";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("correct", 12);

            var user = new User { Id = userId, Email = email, PasswordHash = passwordHash, IsActive = true, FailedLoginAttempts = 4, LockedUntil = null };
            _mockAuthRepository.Setup(x => x.GetUserByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _mockAuthRepository.Setup(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await _handler.Handle(new LoginCommand(email, incorrectPassword, "127.0.0.1"), CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
            _mockAuthRepository.Verify(x => x.UpdateAsync(It.Is<User>(u => u.FailedLoginAttempts == 5 && u.LockedUntil.HasValue), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}