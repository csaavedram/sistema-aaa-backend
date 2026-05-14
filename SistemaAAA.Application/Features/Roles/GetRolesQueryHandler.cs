using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Roles;

public record GetRolesQuery : IRequest<Result<List<RoleDto>>>;

public class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, Result<List<RoleDto>>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly ILogger<GetRolesQueryHandler> _logger;

    public GetRolesQueryHandler(IRoleRepository roleRepository, ILogger<GetRolesQueryHandler> logger)
    {
        _roleRepository = roleRepository;
        _logger = logger;
    }

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
