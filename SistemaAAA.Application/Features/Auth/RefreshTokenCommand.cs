using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Command para rotación de refresh token.
/// </summary>
public record RefreshTokenCommand : IRequest<Result<string>>
{
	/// <summary>
	/// Valor en claro del refresh token recibido.
	/// </summary>
	public string RefreshToken { get; init; } = string.Empty;

	/// <summary>
	/// Usuario esperado, usado para validación adicional.
	/// </summary>
	public Guid? ExpectedUserId { get; init; }
}