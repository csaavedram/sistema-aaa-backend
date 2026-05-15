using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SistemaAAA.Infrastructure.Services;

namespace SistemaAAA.Tests.Unit.Infrastructure;

public class JwtServiceTests
{
    private readonly JwtService _jwtService;
    private const string SecretKey  = "test-secret-key-at-least-32-characters!!";
    private const string Issuer     = "SistemaAAA-Test";
    private const string Audience   = "SistemaAAA-clients-Test";

    public JwtServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"]       = SecretKey,
                ["Jwt:Issuer"]          = Issuer,
                ["Jwt:Audience"]        = Audience,
                ["Jwt:ExpiresMinutes"]  = "60"
            })
            .Build();

        _jwtService = new JwtService(config, new MemoryCache(new MemoryCacheOptions()));
    }

    [Fact]
    public void GenerateAccessToken_WithValidInput_ReturnsJwtWithThreeSegments()
    {
        var token = _jwtService.GenerateAccessToken(
            Guid.NewGuid(), "test@example.com",
            new[] { "User" }, new[] { "users.read" });

        token.Should().NotBeNullOrEmpty();
        token.Split('.').Should().HaveCount(3, "un JWT válido tiene tres segmentos: header.payload.signature");
    }

    [Fact]
    public void ValidateToken_WithValidToken_ReturnsTrueAndExtractsPrincipal()
    {
        var token = _jwtService.GenerateAccessToken(
            Guid.NewGuid(), "test@example.com",
            new[] { "User" }, Array.Empty<string>());

        var isValid = _jwtService.ValidateToken(token, out var principal);

        isValid.Should().BeTrue();
        principal.Should().NotBeNull();
    }

    [Fact]
    public void ValidateToken_WithManipulatedSignature_ReturnsFalse()
    {
        // Arrange: generar token válido y alterar el último carácter de la firma (3er segmento)
        var token = _jwtService.GenerateAccessToken(
            Guid.NewGuid(), "attacker@example.com",
            new[] { "Admin" }, Array.Empty<string>());

        var parts = token.Split('.');
        parts.Should().HaveCount(3, "el token debe tener tres segmentos antes de manipularlo");

        var sig = parts[2];
        // Cambiar el primer carácter (siempre significativo; los bits de padding solo afectan al último)
        var replacedFirst = sig[0] != 'Z' ? 'Z' : 'Y';
        var tamperedToken = $"{parts[0]}.{parts[1]}.{replacedFirst}{sig[1..]}";

        // Act
        var isValid = _jwtService.ValidateToken(tamperedToken, out var principal);

        // Assert
        isValid.Should().BeFalse("una firma alterada debe ser rechazada por HMAC-SHA256");
        principal.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_Expired_ReturnsFalse()
    {
        // Arrange: construir un token expirado directamente para evitar IDX12401
        // (JwtService.GenerateAccessToken lanza esa excepción cuando notBefore >= expires)
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiredJwt = new JwtSecurityToken(
            issuer:             Issuer,
            audience:           Audience,
            claims:             new[] { new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()) },
            notBefore:          DateTime.UtcNow.AddMinutes(-10),
            expires:            DateTime.UtcNow.AddMinutes(-1),
            signingCredentials: credentials);

        var expiredToken = new JwtSecurityTokenHandler().WriteToken(expiredJwt);

        // Act
        var isValid = _jwtService.ValidateToken(expiredToken, out var principal);

        // Assert
        isValid.Should().BeFalse("un token expirado debe ser rechazado (ValidateLifetime = true, ClockSkew = 0)");
        principal.Should().BeNull();
    }
}
