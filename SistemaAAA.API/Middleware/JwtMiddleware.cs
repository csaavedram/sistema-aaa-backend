using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.API.Middleware;

/// <summary>
/// Middleware personalizado para validación y enriquecimiento de contexto JWT.
/// Extrae y valida tokens del header Authorization, asignando las claims al contexto
/// de la solicitud. No bloquea requests sin token; solo enriquece el contexto cuando
/// el token es válido.
/// </summary>
public class JwtMiddleware : IMiddleware
{
    private readonly IJwtService _jwtService;
    private readonly ILogger<JwtMiddleware> _logger;

    /// <summary>
    /// Rutas que no deben validar token (endpoints de autenticación).
    /// </summary>
    private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/v1/auth/login",
        "/api/v1/auth/refresh",
        "/api/v1/auth/forgot-password",
        "/api/v1/auth/reset-password"
    };

    public JwtMiddleware(IJwtService jwtService, ILogger<JwtMiddleware> logger)
    {
        _jwtService = jwtService ?? throw new ArgumentNullException(nameof(jwtService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Procesa la solicitud HTTP, validando el token JWT si está presente
    /// y asignando el principal al contexto.
    /// </summary>
    /// <param name="context">Contexto HTTP de la solicitud.</param>
    /// <param name="next">Delegado para invocar el siguiente middleware.</param>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Ignorar paths excluidos
        if (ShouldExcludePath(context.Request.Path))
        {
            await next(context);
            return;
        }

        try
        {
            // Extraer token del header Authorization
            var authorizationHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(authorizationHeader))
            {
                var token = ExtractToken(authorizationHeader);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    // Validar el token
                    if (_jwtService.ValidateToken(token, out var principal))
                    {
                        // Token válido: enriquecer el contexto
                        context.User = principal!;
                        _logger.LogInformation("Token JWT validado exitosamente");
                    }
                    else
                    {
                        // Token inválido
                        _logger.LogWarning("Token JWT inválido o expirado");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Capturar excepciones internas sin bloquear la solicitud
            _logger.LogError(ex, "Error procesando token JWT");
        }

        // Continuar con el siguiente middleware
        await next(context);
    }

    /// <summary>
    /// Extrae el token del header Authorization, removiendo el prefijo "Bearer ".
    /// </summary>
    /// <param name="authorizationHeader">Valor del header Authorization.</param>
    /// <returns>Token sin el prefijo "Bearer ", o null si el formato es inválido.</returns>
    private static string? ExtractToken(string authorizationHeader)
    {
        const string bearerPrefix = "Bearer ";
        if (authorizationHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return authorizationHeader.Substring(bearerPrefix.Length).Trim();
        }

        return null;
    }

    /// <summary>
    /// Determina si el path de la solicitud debe excluirse de validación JWT.
    /// </summary>
    /// <param name="path">Path de la solicitud.</param>
    /// <returns>True si el path está en la lista de exclusión.</returns>
    private static bool ShouldExcludePath(PathString path)
    {
        return !string.IsNullOrEmpty(path.Value) && ExcludedPaths.Contains(path.Value);
    }
}

/// <summary>
/// Extensiones de aplicación para registrar JwtMiddleware.
/// </summary>
public static class JwtMiddlewareExtensions
{
    /// <summary>
    /// Agrega JwtMiddleware a la pipeline de middleware de la aplicación.
    /// </summary>
    /// <param name="app">Constructor de la aplicación.</param>
    /// <returns>Constructor de la aplicación para encadenamiento.</returns>
    public static IApplicationBuilder UseJwtMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<JwtMiddleware>();
    }
}
