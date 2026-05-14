using Microsoft.EntityFrameworkCore;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de operaciones sobre permisos en EF Core.
/// </summary>
public class PermissionRepository : IPermissionRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="PermissionRepository"/>.
    /// </summary>
    /// <param name="context">Contexto EF Core.</param>
    public PermissionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return _context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    /// <inheritdoc />
    public Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct)
    {
        return Task.FromResult<IEnumerable<Permission>>(
            _context.Permissions
                .AsNoTracking()
                .OrderBy(x => x.Resource)
                .ThenBy(x => x.Action)
                .ToList()
        );
    }

    /// <inheritdoc />
    public Task<IEnumerable<Permission>> GetByRoleIdAsync(Guid roleId, CancellationToken ct)
    {
        return Task.FromResult<IEnumerable<Permission>>(
            _context.RolePermissions
                .AsNoTracking()
                .Where(x => x.RoleId == roleId)
                .Join(
                    _context.Permissions,
                    rp => rp.PermissionId,
                    p => p.Id,
                    (_, permission) => permission)
                .ToList()
        );
    }

    /// <inheritdoc />
    public Task<IEnumerable<Permission>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return Task.FromResult<IEnumerable<Permission>>(
            _context.UserRoles
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Join(
                    _context.RolePermissions,
                    ur => ur.RoleId,
                    rp => rp.RoleId,
                    (_, rp) => rp)
                .Join(
                    _context.Permissions,
                    rp => rp.PermissionId,
                    p => p.Id,
                    (_, permission) => permission)
                .Distinct()
                .ToList()
        );
    }

    /// <inheritdoc />
    public async Task AssignToRoleAsync(Guid roleId, Guid permissionId, CancellationToken ct)
    {
        var rolePermission = new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId
        };

        await _context.RolePermissions.AddAsync(rolePermission, ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RemoveFromRoleAsync(Guid roleId, Guid permissionId, CancellationToken ct)
    {
        var rolePermission = await _context.RolePermissions
            .FirstOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, ct);

        if (rolePermission is null)
        {
            return;
        }

        _context.RolePermissions.Remove(rolePermission);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> IsAssignedToRoleAsync(Guid roleId, Guid permissionId, CancellationToken ct)
    {
        return _context.RolePermissions
            .AsNoTracking()
            .AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, ct);
    }
}
