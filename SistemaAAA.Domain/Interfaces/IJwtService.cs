using System.Security.Claims;

namespace SistemaAAA.Domain.Interfaces;

/// <summary>
/// Servicio para generación y validación de tokens JWT.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Genera un access token JWT con las reclamaciones básicas.
    /// </summary>
    string GenerateAccessToken(Guid userId, string email, string[] roles);

    /// <summary>
    /// Genera un token de refresco (valor en claro, antes de hashearlo si procede).
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Compatibilidad: genera un access token a partir de userId y lista de roles.
    /// </summary>
    string GenerateAccessToken(Guid userId, List<string> roles);

    /// <summary>
    /// Compatibilidad: genera refresh token asociado a userId.
    /// </summary>
    string GenerateRefreshToken(Guid userId);

    /// <summary>
    /// Valida un token y extrae el principal (claims).
    /// </summary>
    bool ValidateToken(string token, out ClaimsPrincipal? principal);

    /// <summary>
    /// Extrae el Id del token (por ejemplo, jti) si existe.
    /// </summary>
    string? ExtractTokenId(string token);
}
