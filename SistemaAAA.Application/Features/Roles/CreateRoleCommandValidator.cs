using FluentValidation;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Validador del command para crear un rol.
/// </summary>
public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("El nombre del rol es obligatorio")
            .WithErrorCode("REQUIRED_FIELD")
            .MinimumLength(2)
            .WithMessage("El nombre del rol debe tener al menos 2 caracteres")
            .MaximumLength(100)
            .WithMessage("El nombre del rol no puede exceder 100 caracteres")
            .Matches(@"^[a-zA-Z0-9_\- ]+$")
            .WithMessage("El nombre del rol solo puede contener letras, números, guiones y espacios")
            .WithErrorCode("INVALID_NAME");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("La descripción del rol es obligatoria")
            .WithErrorCode("REQUIRED_FIELD")
            .MaximumLength(500)
            .WithMessage("La descripción del rol no puede exceder 500 caracteres");

        RuleFor(x => x.CreatedByUserId)
            .NotEmpty()
            .WithMessage("El id del usuario creador es obligatorio")
            .WithErrorCode("REQUIRED_GUID");
    }
}
