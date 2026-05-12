using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaAAA.Domain;

namespace SistemaAAA.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración EF Core para la entidad <see cref="UserRole"/>.
/// </summary>
public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    /// <summary>
    /// Configura la entidad <see cref="UserRole"/>.
    /// </summary>
    /// <param name="builder">Constructor de configuración de entidad.</param>
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");

        builder.HasKey(x => new { x.UserId, x.RoleId });

        builder.Property(x => x.AssignedAt)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_UserRoles_Users_UserId");

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_UserRoles_Roles_RoleId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.AssignedBy)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_UserRoles_Users_AssignedBy");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_UserRoles_UserId");
    }
}
