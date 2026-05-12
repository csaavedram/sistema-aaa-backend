using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaAAA.Domain;

namespace SistemaAAA.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración EF Core para la entidad <see cref="AuditLog"/>.
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <summary>
    /// Configura la entidad <see cref="AuditLog"/>.
    /// </summary>
    /// <param name="builder">Constructor de configuración de entidad.</param>
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.Details)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.EventType);
    }
}