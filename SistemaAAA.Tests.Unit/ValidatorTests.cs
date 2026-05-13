using FluentAssertions;
using FluentValidation.TestHelper;
using SistemaAAA.Application.Features.Auth;
using SistemaAAA.Application.Features.Users;

namespace SistemaAAA.Tests.Unit.Validators;

/// <summary>
/// Tests para LoginCommandValidator.
/// </summary>
public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidData_PassesValidation()
    {
        // Arrange
        var cmd = new LoginCommand("test@example.com", "StrongP@ss1", "127.0.0.1");

        // Act
        var result = _validator.TestValidate(cmd);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyEmail_FailsWithRequiredField()
    {
        // Arrange
        var cmd = new LoginCommand("", "StrongP@ss1", "127.0.0.1");

        // Act
        var result = _validator.TestValidate(cmd);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorCode("REQUIRED_FIELD");
    }

    [Fact]
    public void Validate_WithInvalidEmailFormat_FailsWithInvalidEmail()
    {
        // Arrange
        var cmd = new LoginCommand("not-an-email", "StrongP@ss1", "127.0.0.1");

        // Act
        var result = _validator.TestValidate(cmd);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorCode("INVALID_EMAIL");
    }

    [Fact]
    public void Validate_WithShortPassword_FailsValidation()
    {
        // Arrange
        var cmd = new LoginCommand("test@example.com", "Ab1!", "127.0.0.1");

        // Act
        var result = _validator.TestValidate(cmd);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}

/// <summary>
/// Tests para CreateUserCommandValidator.
/// </summary>
public class CreateUserCommandValidatorTests
{
    private readonly CreateUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidData_PassesValidation()
    {
        // Arrange
        var cmd = new CreateUserCommand("user@example.com", "StrongP@ss1", Guid.NewGuid(), "127.0.0.1");

        // Act
        var result = _validator.TestValidate(cmd);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyEmail_FailsValidation()
    {
        // Arrange
        var cmd = new CreateUserCommand("", "StrongP@ss1", Guid.NewGuid(), null);

        // Act
        var result = _validator.TestValidate(cmd);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorCode("REQUIRED_FIELD");
    }

    [Fact]
    public void Validate_WithWeakPassword_FailsWithWeakPassword()
    {
        // Arrange — sin mayúscula ni carácter especial
        var cmd = new CreateUserCommand("user@example.com", "weakpass1", Guid.NewGuid(), null);

        // Act
        var result = _validator.TestValidate(cmd);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorCode("WEAK_PASSWORD");
    }

    [Fact]
    public void Validate_WithEmptyCreatedByUserId_FailsValidation()
    {
        // Arrange
        var cmd = new CreateUserCommand("user@example.com", "StrongP@ss1", Guid.Empty, null);

        // Act
        var result = _validator.TestValidate(cmd);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CreatedByUserId);
    }
}

/// <summary>
/// Tests para ResetPasswordCommandValidator.
/// </summary>
public class ResetPasswordCommandValidatorTests
{
    private readonly ResetPasswordCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidData_PassesValidation()
    {
        // Arrange
        var cmd = new ResetPasswordCommand("valid-token-string-long-enough", "StrongP@ss1!", null);

        // Act
        var result = _validator.TestValidate(cmd);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyToken_FailsValidation()
    {
        // Arrange
        var cmd = new ResetPasswordCommand("", "StrongP@ss1!", null);

        // Act
        var result = _validator.TestValidate(cmd);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Token)
            .WithErrorCode("REQUIRED_FIELD");
    }

    [Fact]
    public void Validate_WithWeakNewPassword_FailsWithWeakPassword()
    {
        // Arrange — sin mayúscula, número ni especial
        var cmd = new ResetPasswordCommand("valid-token-string-long", "weakpassword", null);

        // Act
        var result = _validator.TestValidate(cmd);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorCode("WEAK_PASSWORD");
    }
}
