using FluentValidation;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Validador del command para actualizar un usuario.
/// </summary>
public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("El id del usuario es obligatorio")
            .WithErrorCode("REQUIRED_GUID");

        RuleFor(x => x.RequestingUserId)
            .NotEmpty()
            .WithMessage("El id del usuario solicitante es obligatorio")
            .WithErrorCode("REQUIRED_GUID");

        When(x => x.Email != null, () =>
        {
            RuleFor(x => x.Email)
                .EmailAddress()
                .WithMessage("Formato de email inválido")
                .WithErrorCode("INVALID_EMAIL")
                .MaximumLength(256);
        });
    }
}
