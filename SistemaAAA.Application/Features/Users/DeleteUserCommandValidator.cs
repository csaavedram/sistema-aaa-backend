using FluentValidation;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Validador del command para eliminar un usuario.
/// </summary>
public class DeleteUserCommandValidator : AbstractValidator<DeleteUserCommand>
{
    public DeleteUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("El id del usuario es obligatorio")
            .WithErrorCode("REQUIRED_GUID");

        RuleFor(x => x.RequestingUserId)
            .NotEmpty()
            .WithMessage("El id del usuario solicitante es obligatorio")
            .WithErrorCode("REQUIRED_GUID");

        RuleFor(x => x.UserId)
            .Must((command, userId) => userId != command.RequestingUserId)
            .When(x => x.UserId != Guid.Empty && x.RequestingUserId != Guid.Empty)
            .WithMessage("Un usuario no puede eliminarse a sí mismo")
            .WithErrorCode("CANNOT_DELETE_SELF");
    }
}
