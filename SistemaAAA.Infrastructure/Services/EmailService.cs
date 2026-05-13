using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Infrastructure.Services;

/// <summary>
/// Implementación de servicio de correo usando System.Net.Mail para SMTP.
/// Configurable mediante IConfiguration con la sección "Email".
/// La validación de configuración se realiza al momento del envío (lazy validation),
/// permitiendo que la app arranque aunque no esté configurado el SMTP.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de correo.
    /// No valida la configuración en el constructor — solo al momento de envío.
    /// </summary>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <param name="logger">Logger para registrar eventos.</param>
    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Envía un correo para reseteo de contraseña con un enlace clickeable.
    /// </summary>
    /// <param name="toEmail">Dirección de correo del destinatario.</param>
    /// <param name="resetLink">Enlace de reseteo de contraseña completo.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <exception cref="InvalidOperationException">
    /// Se lanza si la configuración SMTP está incompleta o si ocurre un error durante el envío.
    /// </exception>
    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink, CancellationToken ct = default)
    {
        var (smtpHost, smtpPort, smtpUser, smtpPassword, fromName, fromAddress) = ReadAndValidateConfig();

        try
        {
            var message = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = "Restablecer contraseña — Sistema AAA",
                Body = BuildPasswordResetEmailBody(resetLink),
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(toEmail));

            using var client = new SmtpClient
            {
                Host = smtpHost,
                Port = smtpPort,
                EnableSsl = true,
                Credentials = new NetworkCredential(smtpUser, smtpPassword)
            };

            using (message)
            {
                await client.SendMailAsync(message, ct);
            }

            _logger.LogInformation("Correo de reseteo de contraseña enviado exitosamente");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar correo de reseteo de contraseña");
            throw new InvalidOperationException("Error procesando solicitud de envío de correo", ex);
        }
    }

    /// <summary>
    /// Envía un correo de bienvenida con las credenciales temporales del usuario.
    /// </summary>
    /// <param name="toEmail">Dirección de correo del destinatario.</param>
    /// <param name="tempPassword">Contraseña temporal asignada al usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <exception cref="InvalidOperationException">
    /// Se lanza si la configuración SMTP está incompleta o si ocurre un error durante el envío.
    /// </exception>
    public async Task SendUserCreatedEmailAsync(string toEmail, string tempPassword, CancellationToken ct = default)
    {
        var (smtpHost, smtpPort, smtpUser, smtpPassword, fromName, fromAddress) = ReadAndValidateConfig();

        try
        {
            var message = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = "Bienvenido a Sistema AAA — Tus credenciales",
                Body = BuildUserCreatedEmailBody(tempPassword),
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(toEmail));

            using var client = new SmtpClient
            {
                Host = smtpHost,
                Port = smtpPort,
                EnableSsl = true,
                Credentials = new NetworkCredential(smtpUser, smtpPassword)
            };

            using (message)
            {
                await client.SendMailAsync(message, ct);
            }

            _logger.LogInformation("Correo de bienvenida enviado exitosamente");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar correo de bienvenida");
            throw new InvalidOperationException("Error procesando solicitud de envío de correo", ex);
        }
    }

    /// <summary>
    /// Lee y valida la configuración SMTP en el momento del envío.
    /// </summary>
    private (string smtpHost, int smtpPort, string smtpUser, string smtpPassword, string fromName, string fromAddress) ReadAndValidateConfig()
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(smtpHost))
            throw new InvalidOperationException("La configuración 'Email:SmtpHost' es obligatoria.");

        var fromAddress = _configuration["Email:FromAddress"];
        if (string.IsNullOrWhiteSpace(fromAddress))
            throw new InvalidOperationException("La configuración 'Email:FromAddress' es obligatoria.");

        var smtpPort = int.TryParse(_configuration["Email:SmtpPort"], out var port) ? port : 587;
        var smtpUser = _configuration["Email:SmtpUser"] ?? string.Empty;
        var smtpPassword = _configuration["Email:SmtpPassword"] ?? string.Empty;
        var fromName = _configuration["Email:FromName"] ?? "Sistema AAA";

        return (smtpHost, smtpPort, smtpUser, smtpPassword, fromName, fromAddress);
    }

    /// <summary>
    /// Construye el cuerpo HTML del correo de reseteo de contraseña.
    /// </summary>
    private static string BuildPasswordResetEmailBody(string resetLink)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; background-color: #f4f4f4; }");
        sb.AppendLine(".container { max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1); }");
        sb.AppendLine(".header { color: #333; text-align: center; }");
        sb.AppendLine(".content { color: #666; margin: 20px 0; }");
        sb.AppendLine(".button { display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; margin-top: 20px; }");
        sb.AppendLine(".footer { color: #999; text-align: center; margin-top: 30px; font-size: 12px; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"container\">");
        sb.AppendLine("<div class=\"header\"><h1>Restablecer Contraseña</h1></div>");
        sb.AppendLine("<div class=\"content\">");
        sb.AppendLine("<p>Hola,</p>");
        sb.AppendLine("<p>Recibimos una solicitud para restablecer tu contraseña. Haz clic en el siguiente enlace para continuar:</p>");
        sb.AppendLine($"<a href=\"{EscapeHtml(resetLink)}\" class=\"button\">Restablecer contraseña</a>");
        sb.AppendLine("<p>Si no solicitaste este cambio, puedes ignorar este correo.</p>");
        sb.AppendLine("<p>Por razones de seguridad, este enlace expira en 1 hora.</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"footer\"><p>&copy; 2026 Sistema AAA. Todos los derechos reservados.</p></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    /// <summary>
    /// Construye el cuerpo HTML del correo de bienvenida.
    /// </summary>
    private static string BuildUserCreatedEmailBody(string tempPassword)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head><meta charset=\"UTF-8\"></head>");
        sb.AppendLine("<body style=\"font-family: Arial, sans-serif; color: #333;\">");
        sb.AppendLine("<p>Tu cuenta en Sistema AAA ha sido creada correctamente.</p>");
        sb.AppendLine("<p>Usa la siguiente contraseña temporal para iniciar sesión por primera vez:</p>");
        sb.AppendLine($"<p><strong>{EscapeHtml(tempPassword)}</strong></p>");
        sb.AppendLine("<p>Te recomendamos cambiarla inmediatamente después de ingresar.</p>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string EscapeHtml(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
