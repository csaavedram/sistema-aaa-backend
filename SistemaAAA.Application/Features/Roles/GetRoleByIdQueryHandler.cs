using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Roles;

public record GetRoleByIdQuery(Guid RoleId) : IRequest<Result<RoleDto>>;

public class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, Result<RoleDto>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly ILogger<GetRoleByIdQueryHandler> _logger;

    public GetRoleByIdQueryHandler(IRoleRepository roleRepository, ILogger<GetRoleByIdQueryHandler> logger)
    {
        _roleRepository = roleRepository;
        _logger = logger;
    }

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
