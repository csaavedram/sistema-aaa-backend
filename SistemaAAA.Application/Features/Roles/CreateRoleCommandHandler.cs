using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Roles;

/// <summary>
/// Handler que crea un nuevo rol.
/// </summary>
public class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, Result<RoleDto>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<CreateRoleCommandHandler> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="CreateRoleCommandHandler"/>.
    /// </summary>
    public CreateRoleCommandHandler(
        IRoleRepository roleRepository,
        IAuditRepository auditRepository,
        ILogger<CreateRoleCommandHandler> logger)
    {
        _roleRepository = roleRepository;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la creación de un rol.
    /// </summary>
    public async Task<Result<RoleDto>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var normalizedName = request.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                _logger.LogWarning("Attempt to create role with empty name");
                return Result<RoleDto>.Failure("ROLE_NAME_TAKEN", "Nombre de rol inválido");
            }

            var existingRole = await _roleRepository.GetByNameAsync(normalizedName, cancellationToken);
            if (existingRole is not null)
            {
                _logger.LogWarning("Attempt to create duplicated role name {RoleName}", normalizedName);
                return Result<RoleDto>.Failure("ROLE_NAME_TAKEN", "Ya existe un rol con ese nombre");
            }

            var role = new Role
            {
                Id = Guid.NewGuid(),
                Name = normalizedName,
                Description = request.Description,
                IsSystem = false
            };

            await _roleRepository.CreateAsync(role, cancellationToken);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.CreatedByUserId,
                EventType = "ROLE_CREATED",
                Resource = "Role",
                Details = $"Rol {role.Id} creado",
                IpAddress = request.IpAddress,
                CreatedAt = DateTime.UtcNow
            };

            await _auditRepository.InsertAsync(auditLog, cancellationToken);

            _logger.LogInformation("Role {RoleId} created by admin {AdminId}", role.Id, request.CreatedByUserId);

            return Result<RoleDto>.Success(new RoleDto(role.Id, role.Name, role.Description, role.IsSystem));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role");
            return Result<RoleDto>.Failure("INTERNAL_ERROR", "Error creando rol");
        }
    }
}
