using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Auth;

public record ResetPasswordCommand(string Token, string NewPassword, string? IpAddress) : IRequest<Result<bool>>;
