using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SistemaAAA.Infrastructure.Persistence;

/// <summary>
/// Factory para construir ApplicationDbContext en tiempo de diseño.
/// Utilizado por Entity Framework Core migrations sin dependencias de Program.cs.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    /// <summary>
    /// Crea una instancia de ApplicationDbContext para EF Core migrations.
    /// Lee la configuración desde appsettings.json del proyecto API.
    /// </summary>
    /// <param name="args">Argumentos de línea de comandos (no utilizados).</param>
    /// <returns>Instancia configurada de ApplicationDbContext.</returns>
    /// <exception cref="InvalidOperationException">
    /// Se lanza si la cadena de conexión no está configurada en appsettings.json.
    /// </exception>
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Construir el path al proyecto API donde reside appsettings.json
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "SistemaAAA.API");
        var configPath = Path.Combine(basePath, "appsettings.json");

        // Construir la configuración desde appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        // Obtener la cadena de conexión
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "La cadena de conexión 'DefaultConnection' no está configurada en appsettings.json");

        // Construir DbContextOptions
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}

