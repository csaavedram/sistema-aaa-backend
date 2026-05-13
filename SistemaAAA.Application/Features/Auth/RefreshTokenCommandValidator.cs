using FluentValidation;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Validador del command para rotación de refresh token.
/// </summary>
public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("El refresh token es obligatorio")
            .WithErrorCode("REQUIRED_FIELD");
    }
}
