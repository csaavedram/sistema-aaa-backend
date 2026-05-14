using FluentValidation;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Validador del command para reseteo de contraseña.
/// </summary>
public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("El token es obligatorio")
            .WithErrorCode("REQUIRED_FIELD")
            .MinimumLength(10)
            .WithMessage("El token es inválido");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("La contraseña es obligatoria")
            .WithErrorCode("REQUIRED_FIELD")
            .MinimumLength(8)
            .WithMessage("La contraseña debe tener al menos 8 caracteres")
            .MaximumLength(128)
            .Matches(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$")
            .WithMessage("La contraseña debe tener mayúscula, número y carácter especial")
            .WithErrorCode("WEAK_PASSWORD");
    }
}
