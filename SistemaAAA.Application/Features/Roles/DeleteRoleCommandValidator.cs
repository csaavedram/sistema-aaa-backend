using FluentValidation;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Validador del command para eliminar un rol.
/// </summary>
public class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty()
            .WithMessage("El id del rol es obligatorio")
            .WithErrorCode("REQUIRED_GUID");

        RuleFor(x => x.DeletedByUserId)
            .NotEmpty()
            .WithMessage("El id del usuario que elimina es obligatorio")
            .WithErrorCode("REQUIRED_GUID");
    }
}
