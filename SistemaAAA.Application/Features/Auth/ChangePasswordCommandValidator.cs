using FluentValidation;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Validador del command para cambio de contraseña.
/// </summary>
public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    /// <summary>
    /// Inicializa una nueva instancia del validador.
    /// </summary>
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("El id del usuario es obligatorio")
            .WithErrorCode("REQUIRED_GUID");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("La contraseña actual es obligatoria")
            .WithErrorCode("REQUIRED_FIELD")
            .MinimumLength(1)
            .WithMessage("La contraseña actual es obligatoria");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("La nueva contraseña es obligatoria")
            .WithErrorCode("REQUIRED_FIELD")
            .MinimumLength(8)
            .WithMessage("La contraseña debe tener al menos 8 caracteres")
            .MaximumLength(128)
            .Matches(@"^(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*()\-_=+\[\{\]}\\|;:'"",<.>/?`~]).{8,}$")
            .WithMessage("La contraseña debe tener mayúscula, número y carácter especial")
            .WithErrorCode("WEAK_PASSWORD");
    }
}