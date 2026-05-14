using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Roles;

public record GetRoleByIdQuery(Guid RoleId) : IRequest<Result<RoleDto>>;

/// <summary>
/// Maneja la consulta para obtener un rol por su identificador.
/// </summary>
public class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, Result<RoleDto>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly ILogger<GetRoleByIdQueryHandler> _logger;

    public GetRoleByIdQueryHandler(IRoleRepository roleRepository, ILogger<GetRoleByIdQueryHandler> logger)
    {
        _roleRepository = roleRepository;
        _logger = logger;
    }

    /// <summary>Busca y retorna el rol correspondiente al identificador indicado.</summary>
    /// <param name="request">Consulta con el identificador del rol.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    public async Task<Result<RoleDto>> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
            if (role is null)
            {
                return Result<RoleDto>.Failure("ROLE_NOT_FOUND", "Rol no encontrado");
            }

            var dto = new RoleDto(role.Id, role.Name, role.Description, role.IsSystem);
            _logger.LogInformation("Retrieved role {RoleId}", request.RoleId);
            return Result<RoleDto>.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role {RoleId}", request.RoleId);
            return Result<RoleDto>.Failure("INTERNAL_ERROR", "Error obteniendo el rol");
        }
    }
}
