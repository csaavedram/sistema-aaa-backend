using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Infrastructure.Services;

/// <summary>
/// Servicio para generación y validación de tokens JWT.
/// </summary>
public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public JwtService(IConfiguration configuration, IMemoryCache cache)
    {
        _configuration = configuration;
        _cache = cache;
    }

    /// <inheritdoc/>
    public string GenerateAccessToken(Guid userId, string email, string[] roles, string[] permissions)
    {
        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey no configurada.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
        };

        // Un claim por rol para compatibilidad con IsInRole() y [Authorize(Roles="...")]
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        // Un claim por permiso para policies granulares [Authorize(Policy = "...")]
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));

        var expiresMinutes = int.TryParse(_configuration["Jwt:ExpiresMinutes"], out var min) ? min : 60;
        
        var token = new JwtSecurityToken(
            issuer:             _configuration["Jwt:Issuer"],
            audience:           _configuration["Jwt:Audience"],
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc/>
    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    /// <summary>
    /// Compatibilidad: genera un access token con roles list.
    /// </summary>
    public string GenerateAccessToken(Guid userId, List<string> roles)
        => GenerateAccessToken(userId, string.Empty, roles.ToArray(), []);

    /// <summary>
    /// Compatibilidad: genera refresh token asociado a un usuario (ignorando userId internamente).
    /// </summary>
    public string GenerateRefreshToken(Guid userId)
        => GenerateRefreshToken();

    /// <inheritdoc/>
    public bool ValidateToken(string token, out ClaimsPrincipal? principal)
    {
        principal = null;

        var secretKey = _configuration["Jwt:SecretKey"];
        if (string.IsNullOrEmpty(secretKey)) return false;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = key,

            ValidateIssuer   = true,
            ValidIssuer      = _configuration["Jwt:Issuer"],

            ValidateAudience = true,
            ValidAudience    = _configuration["Jwt:Audience"],

            ValidateLifetime = true,
            ClockSkew        = TimeSpan.Zero   // sin margen: expiración exacta
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            principal = handler.ValidateToken(token, validationParams, out var validatedToken);

            // Verificar que el algoritmo sea el esperado (evitar el ataque "alg: none")
            if (validatedToken is not JwtSecurityToken jwt ||
                !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                principal = null;
                return false;
            }

            // Verificar revocación en caché (token bloqueado por logout / rotación)
            var jti = jwt.Id;
            if (!string.IsNullOrEmpty(jti) && _cache.TryGetValue($"revoked:{jti}", out _))
            {
                principal = null;
                return false;
            }

            return true;
        }
        catch (SecurityTokenException)
        {
            return false;   // firma inválida, expirado, issuer/audience incorrectos, etc.
        }
    }

    /// <inheritdoc/>
    public string? ExtractTokenId(string token)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return jwt.Id; // equivale a jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value
    }
}