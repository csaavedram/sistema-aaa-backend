using System.Threading;
using System.Threading.Tasks;

namespace SistemaAAA.Domain.Interfaces;

/// <summary>
/// Servicio para envío de correos. La implementación concreta se proveerá en Infrastructure.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Envía un correo para reseteo de contraseña con el enlace proporcionado.
    /// </summary>
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink, CancellationToken ct = default);
}
