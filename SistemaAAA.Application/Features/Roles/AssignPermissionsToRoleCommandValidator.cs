using FluentValidation;
using SistemaAAA.Application.Features.Roles;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Validador para el comando AssignPermissionsToRoleCommand.
/// </summary>
public class AssignPermissionsToRoleCommandValidator : AbstractValidator<AssignPermissionsToRoleCommand>
{
    public AssignPermissionsToRoleCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty()
            .WithMessage("El ID del rol es requerido");

        RuleFor(x => x.PermissionIds)
            .NotNull()
            .WithMessage("La lista de permisos no puede ser nula")
            .NotEmpty()
            .WithMessage("Debe proporcionar al menos un permiso");

        RuleFor(x => x.AssignedByUserId)
            .NotEmpty()
            .WithMessage("El ID del usuario que asigna es requerido");
    }
}
