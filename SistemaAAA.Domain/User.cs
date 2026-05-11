namespace SistemaAAA.Domain;

/// <summary>
/// Representa un usuario del sistema.
/// Contiene información de autenticación y estado del usuario.
/// </summary>
public class User
{
    /// <summary>
    /// Identificador único del usuario.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Correo electrónico del usuario, usado como identificador único para inicio de sesión.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Hash de la contraseña del usuario.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Indica si el usuario está activo en el sistema.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Contador de intentos de inicio de sesión fallidos.
    /// Se reinicia después de un inicio de sesión exitoso.
    /// </summary>
    public int FailedLoginAttempts { get; set; } = 0;

    /// <summary>
    /// Fecha y hora hasta la cual el usuario está bloqueado por intentos de inicio de sesión fallidos.
    /// Nulo si el usuario no está bloqueado.
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// Fecha y hora de creación del usuario.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Fecha y hora de la última actualización del usuario.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
