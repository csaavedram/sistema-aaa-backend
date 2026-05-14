using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Roles;

public record GetRolesQuery : IRequest<Result<List<RoleDto>>>;

/// <summary>
/// Maneja la consulta para obtener todos los roles del sistema.
/// </summary>
public class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, Result<List<RoleDto>>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly ILogger<GetRolesQueryHandler> _logger;

    public GetRolesQueryHandler(IRoleRepository roleRepository, ILogger<GetRolesQueryHandler> logger)
    {
        _roleRepository = roleRepository;
        _logger = logger;
    }

    /// <summary>Devuelve la lista completa de roles registrados.</summary>
    /// <param name="request">Consulta sin parámetros adicionales.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    public async Task<Result<List<RoleDto>>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var roles = await _roleRepository.GetAllAsync(cancellationToken);
            var dtos = roles
                .Select(r => new RoleDto(r.Id, r.Name, r.Description, r.IsSystem))
                .ToList();

            _logger.LogInformation("Retrieved {Count} roles", dtos.Count);
            return Result<List<RoleDto>>.Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all roles");
            return Result<List<RoleDto>>.Failure("INTERNAL_ERROR", "Error obteniendo roles");
        }
    }
}
