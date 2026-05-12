using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Auth;

/// <summary>
/// Command que inicia el flujo de recuperación de contraseña.
/// </summary>
public record ForgotPasswordCommand(string Email, string? IpAddress) : IRequest<Result<bool>>;
