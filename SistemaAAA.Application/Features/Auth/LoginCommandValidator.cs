using FluentValidation;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Validador del command de login.
/// </summary>
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("El email es obligatorio")
            .WithErrorCode("REQUIRED_FIELD")
            .EmailAddress()
            .WithMessage("Formato de email inválido")
            .WithErrorCode("INVALID_EMAIL")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("La contraseña es obligatoria")
            .WithErrorCode("REQUIRED_FIELD")
            .MinimumLength(8)
            .WithMessage("La contraseña debe tener al menos 8 caracteres")
            .MaximumLength(128);
    }
}
