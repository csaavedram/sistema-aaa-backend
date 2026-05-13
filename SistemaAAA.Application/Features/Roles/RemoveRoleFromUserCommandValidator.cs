using FluentValidation;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Validador del command para quitar un rol a un usuario.
/// </summary>
/// <remarks>
/// NOTA: La regla de negocio que valida si el usuario puede remover un rol específico
/// (ej: no remover el rol Admin a un usuario) es responsabilidad del handler,
/// no del validador. El validador solo valida la estructura del comando.
/// </remarks>
public class RemoveRoleFromUserCommandValidator : AbstractValidator<RemoveRoleFromUserCommand>
{
    public RemoveRoleFromUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("El id del usuario es obligatorio")
            .WithErrorCode("REQUIRED_GUID");

        RuleFor(x => x.RoleId)
            .NotEmpty()
            .WithMessage("El id del rol es obligatorio")
            .WithErrorCode("REQUIRED_GUID");

        RuleFor(x => x.RemovedByUserId)
            .NotEmpty()
            .WithMessage("El id del usuario que remueve es obligatorio")
            .WithErrorCode("REQUIRED_GUID");
    }
}
