using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaAAA.Domain;

namespace SistemaAAA.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración EF Core para la entidad <see cref="Role"/>.
/// </summary>
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <summary>
    /// Configura la entidad <see cref="Role"/>.
    /// </summary>
    /// <param name="builder">Constructor de configuración de entidad.</param>
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.IsSystem)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .HasDatabaseName("IX_Roles_Name");
    }
}
