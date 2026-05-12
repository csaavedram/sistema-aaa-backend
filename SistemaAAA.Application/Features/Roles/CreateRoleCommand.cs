using MediatR;
using SistemaAAA.Application.Common;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Comando para crear un nuevo rol.
/// </summary>
public record CreateRoleCommand(
    string Name,
    string Description,
    Guid CreatedByUserId,
    string? IpAddress) : IRequest<Result<RoleDto>>;

/// <summary>
/// DTO seguro de rol.
/// </summary>
public record RoleDto(
    Guid Id,
    string Name,
    string Description,
    bool IsSystem);
