using FluentValidation;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Validador del command para crear un usuario.
/// </summary>
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
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
            .MaximumLength(128)
            .Matches(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$")
            .WithMessage("La contraseña debe tener mayúscula, número y carácter especial")
            .WithErrorCode("WEAK_PASSWORD");

        RuleFor(x => x.CreatedByUserId)
            .NotEmpty()
            .WithMessage("El id del creador es obligatorio")
            .WithErrorCode("REQUIRED_GUID");
    }
}
