using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaAAA.Domain;

namespace SistemaAAA.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración EF Core para la entidad <see cref="PasswordResetToken"/>.
/// </summary>
public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    /// <summary>
    /// Configura la entidad <see cref="PasswordResetToken"/>.
    /// </summary>
    /// <param name="builder">Constructor de configuración de entidad.</param>
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.Property(x => x.IsUsed)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UsedAt);

        // FK a Users con Cascade delete
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("FK_PasswordResetTokens_Users_UserId")
            .OnDelete(DeleteBehavior.Cascade);

        // Índice para búsqueda de tokens activos (no usados y no expirados)
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_PasswordResetTokens_UserId_Active")
            .HasFilter("[IsUsed] = 0 AND [ExpiresAt] > GETUTCDATE()");

        // Índice para limpieza de tokens expirados
        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("IX_PasswordResetTokens_ExpiresAt");
    }
}
