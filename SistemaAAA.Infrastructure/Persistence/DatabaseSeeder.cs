using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    // GUIDs fijos para roles — garantizan idempotencia entre ambientes
    private static readonly Guid AdminRoleId   = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AuditorRoleId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid UserRoleId    = new("00000000-0000-0000-0000-000000000003");

    // GUIDs fijos para permisos
    private static readonly Dictionary<string, Guid> PermissionIds = new()
    {
        ["users.create"]       = new("00000000-0000-0000-0001-000000000001"),
        ["users.read"]         = new("00000000-0000-0000-0001-000000000002"),
        ["users.update"]       = new("00000000-0000-0000-0001-000000000003"),
        ["users.delete"]       = new("00000000-0000-0000-0001-000000000004"),
        ["roles.create"]       = new("00000000-0000-0000-0002-000000000001"),
        ["roles.read"]         = new("00000000-0000-0000-0002-000000000004"),
        ["roles.delete"]       = new("00000000-0000-0000-0002-000000000002"),
        ["roles.assign"]       = new("00000000-0000-0000-0002-000000000003"),
        ["permissions.assign"] = new("00000000-0000-0000-0003-000000000001"),
        ["audit.read"]         = new("00000000-0000-0000-0004-000000000001"),
    };

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var context       = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var logger        = serviceProvider.GetRequiredService<ILoggerFactory>()
                                           .CreateLogger("DatabaseSeeder");

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            await SeedRolesAsync(context, logger);
            await SeedPermissionsAsync(context, logger);
            await SeedRolePermissionsAsync(context, logger);
            await SeedAdminUserAsync(context, passwordHasher, configuration, logger);

            await transaction.CommitAsync();
            logger.LogInformation("Database seed completed successfully");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Database seed failed — transaction rolled back");
            throw;
        }
    }

    private static async Task SeedRolesAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.Roles.AnyAsync(r => r.Name == "Admin"))
        {
            logger.LogInformation("Roles already seeded — skipping");
            return;
        }

        var roles = new[]
        {
            new Role { Id = AdminRoleId,   Name = "Admin",   Description = "Administrador del sistema",          IsSystem = true },
            new Role { Id = AuditorRoleId, Name = "Auditor", Description = "Acceso de solo lectura a auditoría", IsSystem = true },
            new Role { Id = UserRoleId,    Name = "User",    Description = "Usuario estándar",                   IsSystem = true },
        };

        await context.Roles.AddRangeAsync(roles);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} roles", roles.Length);
    }

    private static async Task SeedPermissionsAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.Permissions.AnyAsync())
        {
            logger.LogInformation("Permissions already seeded — skipping");
            return;
        }

        var permissions = new[]
        {
            new Permission { Id = PermissionIds["users.create"],       Name = "users.create",       Resource = "User",       Action = "Create" },
            new Permission { Id = PermissionIds["users.read"],         Name = "users.read",         Resource = "User",       Action = "Read"   },
            new Permission { Id = PermissionIds["users.update"],       Name = "users.update",       Resource = "User",       Action = "Update" },
            new Permission { Id = PermissionIds["users.delete"],       Name = "users.delete",       Resource = "User",       Action = "Delete" },
            new Permission { Id = PermissionIds["roles.create"],       Name = "roles.create",       Resource = "Role",       Action = "Create" },
            new Permission { Id = PermissionIds["roles.read"],         Name = "roles.read",         Resource = "Role",       Action = "Read"   },
            new Permission { Id = PermissionIds["roles.delete"],       Name = "roles.delete",       Resource = "Role",       Action = "Delete" },
            new Permission { Id = PermissionIds["roles.assign"],       Name = "roles.assign",       Resource = "Role",       Action = "Assign" },
            new Permission { Id = PermissionIds["permissions.assign"], Name = "permissions.assign", Resource = "Permission", Action = "Assign" },
            new Permission { Id = PermissionIds["audit.read"],         Name = "audit.read",         Resource = "AuditLog",   Action = "Read"   },
        };

        await context.Permissions.AddRangeAsync(permissions);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} permissions", permissions.Length);
    }

    private static async Task SeedRolePermissionsAsync(ApplicationDbContext context, ILogger logger)
    {
        // Admin recibe todos los permisos
        foreach (var permId in PermissionIds.Values)
        {
            if (!await context.RolePermissions.AnyAsync(rp => rp.RoleId == AdminRoleId && rp.PermissionId == permId))
            {
                await context.RolePermissions.AddAsync(
                    new RolePermission { RoleId = AdminRoleId, PermissionId = permId });
            }
        }

        // Auditor recibe: users.read, roles.read, audit.read
        var auditorPermissions = new[] { PermissionIds["users.read"], PermissionIds["roles.read"], PermissionIds["audit.read"] };
        foreach (var permId in auditorPermissions)
        {
            if (!await context.RolePermissions.AnyAsync(rp => rp.RoleId == AuditorRoleId && rp.PermissionId == permId))
            {
                await context.RolePermissions.AddAsync(
                    new RolePermission { RoleId = AuditorRoleId, PermissionId = permId });
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded role permissions");
    }

    private static async Task SeedAdminUserAsync(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger logger)
    {
        var hasAdmin = await context.Users.AnyAsync(u =>
            context.UserRoles.Any(ur => ur.UserId == u.Id &&
            context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Admin")));

        if (hasAdmin)
        {
            logger.LogInformation("Admin user already exists — skipping");
            return;
        }

        var email    = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            logger.LogError("Admin user email or password is not configured");
            return;
        }

        var now = DateTime.UtcNow;
        var adminUser = new User
        {
            Id                  = Guid.NewGuid(),
            Email               = email,
            PasswordHash        = passwordHasher.Hash(password),
            IsActive            = true,
            FailedLoginAttempts = 0,
            CreatedAt           = now,
            UpdatedAt           = now,
        };

        await context.Users.AddAsync(adminUser);
        await context.SaveChangesAsync();

        await context.UserRoles.AddAsync(new UserRole
        {
            UserId     = adminUser.Id,
            RoleId     = AdminRoleId,
            AssignedAt = now,
            AssignedBy = adminUser.Id,
        });

        await context.SaveChangesAsync();
        logger.LogInformation("Admin user seeded with email: {Email}", email);
    }
}
