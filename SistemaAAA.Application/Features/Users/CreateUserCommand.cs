using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Comando para crear un nuevo usuario.
/// </summary>
public record CreateUserCommand(
    string Email,
    string Password,
    Guid CreatedByUserId,
    string? IpAddress) : IRequest<Result<CreateUserResponse>>;

/// <summary>
/// Respuesta del comando de creación de usuario.
/// </summary>
public record CreateUserResponse(
    Guid UserId,
    string Email,
    DateTime CreatedAt);
