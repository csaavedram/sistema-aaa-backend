using Microsoft.EntityFrameworkCore;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación del repositorio de roles.
/// </summary>
public class RoleRepository : IRoleRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="RoleRepository"/>.
    /// </summary>
    /// <param name="context">Contexto EF Core.</param>
    public RoleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return _context.Roles.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    /// <inheritdoc />
    public Task<Role?> GetByNameAsync(string name, CancellationToken ct)
    {
        return _context.Roles.FirstOrDefaultAsync(x => x.Name == name, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Role>> GetAllAsync(CancellationToken ct)
    {
        return await _context.Roles
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task CreateAsync(Role role, CancellationToken ct)
    {
        await _context.Roles.AddAsync(role, ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (role is null)
        {
            return;
        }

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> IsAssignedToUserAsync(Guid roleId, Guid userId, CancellationToken ct)
    {
        return _context.UserRoles.AnyAsync(x => x.RoleId == roleId && x.UserId == userId, ct);
    }

    /// <inheritdoc />
    public async Task AssignToUserAsync(Guid roleId, Guid userId, Guid assignedBy, CancellationToken ct)
    {
        var exists = await _context.UserRoles.AnyAsync(x => x.RoleId == roleId && x.UserId == userId, ct);
        if (exists)
        {
            return;
        }

        var userRole = new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            AssignedBy = assignedBy,
            AssignedAt = DateTime.UtcNow
        };

        await _context.UserRoles.AddAsync(userRole, ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RemoveFromUserAsync(Guid roleId, Guid userId, CancellationToken ct)
    {
        var userRole = await _context.UserRoles
            .FirstOrDefaultAsync(x => x.RoleId == roleId && x.UserId == userId, ct);

        if (userRole is null)
        {
            return;
        }

        _context.UserRoles.Remove(userRole);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Role>> GetRolesForUserAsync(Guid userId, CancellationToken ct)
    {
        return await _context.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Join(_context.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (_, role) => role)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }
}
