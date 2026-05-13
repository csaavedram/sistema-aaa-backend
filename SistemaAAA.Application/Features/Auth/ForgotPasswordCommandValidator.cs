using FluentValidation;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Validador del command para inicio de recuperación de contraseña.
/// </summary>
public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("El email es obligatorio")
            .WithErrorCode("REQUIRED_FIELD")
            .EmailAddress()
            .WithMessage("Formato de email inválido")
            .WithErrorCode("INVALID_EMAIL")
            .MaximumLength(256);
    }
}
