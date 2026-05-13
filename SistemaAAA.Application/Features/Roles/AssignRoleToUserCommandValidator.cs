using FluentValidation;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Validador del command para asignar un rol a un usuario.
/// </summary>
public class AssignRoleToUserCommandValidator : AbstractValidator<AssignRoleToUserCommand>
{
    public AssignRoleToUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("El id del usuario es obligatorio")
            .WithErrorCode("REQUIRED_GUID");

        RuleFor(x => x.RoleId)
            .NotEmpty()
            .WithMessage("El id del rol es obligatorio")
            .WithErrorCode("REQUIRED_GUID");

        RuleFor(x => x.AssignedByUserId)
            .NotEmpty()
            .WithMessage("El id del usuario que asigna es obligatorio")
            .WithErrorCode("REQUIRED_GUID");
    }
}
