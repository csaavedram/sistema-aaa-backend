using Microsoft.EntityFrameworkCore;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación del repositorio de usuarios.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="UserRepository"/>.
    /// </summary>
    /// <param name="context">Contexto EF Core.</param>
    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return _context.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<User>> GetAllAsync(int page, int pageSize, CancellationToken ct)
    {
        var validPage = page > 0 ? page : 1;
        var validPageSize = pageSize > 0 ? pageSize : 50;

        return await _context.Users
            .OrderBy(x => x.Email)
            .Skip((validPage - 1) * validPageSize)
            .Take(validPageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task CreateAsync(User user, CancellationToken ct)
    {
        await _context.Users.AddAsync(user, ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(User user, CancellationToken ct)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null)
        {
            return;
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> ExistsWithEmailAsync(string email, CancellationToken ct)
    {
        return _context.Users.AnyAsync(x => x.Email == email, ct);
    }

    /// <inheritdoc />
    public Task<int> GetAdminCountAsync(CancellationToken ct)
    {
        return _context.UserRoles
            .Join(
                _context.Roles,
                ur => ur.RoleId,
                r => r.Id,
                (_, r) => r.Name)
            .CountAsync(roleName => roleName == "Admin", ct);
    }
}
