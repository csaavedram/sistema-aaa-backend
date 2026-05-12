using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Query para obtener los detalles de un usuario específico.
/// </summary>
public record GetUserQuery(Guid UserId) : IRequest<Result<UserDto>>;

/// <summary>
/// Data Transfer Object para información segura de usuario.
/// </summary>
/// <remarks>
/// CRÍTICO: No expone PasswordHash, FailedLoginAttempts ni LockedUntil.
/// </remarks>
public record UserDto(
    Guid Id,
    string Email,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
