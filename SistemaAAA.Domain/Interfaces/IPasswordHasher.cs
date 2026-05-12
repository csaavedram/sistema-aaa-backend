namespace SistemaAAA.Domain.Interfaces;

/// <summary>
/// Abstracción para operaciones de hash/verify de contraseñas.
/// Implementaciones concretas deben residir en la capa Infrastructure.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Genera un hash seguro para la contraseña en texto plano.
    /// </summary>
    string Hash(string plain);

    /// <summary>
    /// Verifica que la contraseña en texto plano coincida con el hash proporcionado.
    /// </summary>
    bool Verify(string plain, string hash);
}
