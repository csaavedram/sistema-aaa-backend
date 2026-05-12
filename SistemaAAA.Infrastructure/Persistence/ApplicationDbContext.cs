using Microsoft.EntityFrameworkCore;
using SistemaAAA.Domain;

namespace SistemaAAA.Infrastructure.Persistence;

/// <summary>
/// Contexto principal de Entity Framework Core para el sistema AAA.
/// </summary>
public class ApplicationDbContext : DbContext
{
    /// <summary>
    /// Inicializa una nueva instancia de <see cref="ApplicationDbContext"/>.
    /// </summary>
    /// <param name="options">Opciones de configuración del contexto.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Usuarios del sistema.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Roles del sistema.
    /// </summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>
    /// Relación entre usuarios y roles.
    /// </summary>
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    /// <summary>
    /// Permisos disponibles.
    /// </summary>
    public DbSet<Permission> Permissions => Set<Permission>();

    /// <summary>
    /// Relación entre roles y permisos.
    /// </summary>
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    /// <summary>
    /// Tokens de refresco.
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Registros de auditoría.
    /// </summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// Configura el modelo EF Core.
    /// </summary>
    /// <param name="modelBuilder">Constructor del modelo.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}