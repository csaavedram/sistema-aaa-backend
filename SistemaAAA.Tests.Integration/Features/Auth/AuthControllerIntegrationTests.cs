using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaAAA.API;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;
using SistemaAAA.Infrastructure.Persistence;
using Xunit;

namespace SistemaAAA.Tests.Integration.Auth;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"]       = "test-secret-key-at-least-32-characters!!",
                ["Jwt:Issuer"]          = "SistemaAAA-Test",
                ["Jwt:Audience"]        = "SistemaAAA-clients-Test",
                ["Jwt:ExpiresMinutes"]  = "60",
                ["Seed:RunOnStartup"]   = "false"
            });
        });
    }
}

public class AuthControllerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public AuthControllerIntegrationTests(TestWebApplicationFactory factory)
    {
        var dbName = "TestDb_" + Guid.NewGuid().ToString();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(dbName));
            });
        });

        _client = _factory.CreateClient();
    }

    private async Task SeedUserAsync(User user, string clearPassword)
    {
        using var scope = _factory.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        user.PasswordHash = hasher.Hash(clearPassword);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    // ─── Helpers de contrato ──────────────────────────────────────────────────

    private static JsonElement GetLoginData(JsonDocument doc)
    {
        var root = doc.RootElement;
        // La API devuelve { success, data: { accessToken, expiresIn, userId, roles } }
        if (root.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null)
            return data;
        // Fallback para estructura plana
        return root;
    }

    private static string? ExtractToken(JsonDocument doc)
    {
        var data = GetLoginData(doc);
        return data.TryGetProperty("accessToken", out var t) ? t.GetString() : null;
    }

    // ─── Tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostLogin_WithValidCredentials_Returns200WithExpectedContractAndCookie()
    {
        // Arrange
        var password = "Test@123!";
        var user = new User { Id = Guid.NewGuid(), Email = "contract@example.com", IsActive = true };
        await SeedUserAsync(user, password);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { user.Email, Password = password });

        // Assert — código de estado
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Respuesta: {content}");

        // Assert — estructura exacta del contrato JSON
        using var doc  = JsonDocument.Parse(content);
        var root       = doc.RootElement;

        root.GetProperty("success").GetBoolean()
            .Should().BeTrue("el campo 'success' debe ser true en una respuesta exitosa");

        var data = root.GetProperty("data");
        data.GetProperty("accessToken").GetString()
            .Should().NotBeNullOrEmpty("se debe devolver un access token");
        data.GetProperty("expiresIn").GetInt32()
            .Should().BeGreaterThan(0, "expiresIn debe ser positivo");
        data.GetProperty("userId").GetString()
            .Should().NotBeNullOrEmpty("se debe devolver el userId");
        data.GetProperty("roles").ValueKind
            .Should().Be(JsonValueKind.Array, "roles debe ser un array JSON");

        // Assert — sin campos sensibles en el body
        content.Should().NotContain("passwordHash",  "el hash de contraseña no debe exponerse");
        content.Should().NotContain("PasswordHash",  "el hash de contraseña no debe exponerse (PascalCase)");
        // El refresh token va exclusivamente en la cookie HttpOnly
        content.Should().NotContain("\"refreshToken\"", "el refresh token no debe estar en el body");

        // Assert — cookie de refresh token HttpOnly establecida
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue("debe setearse la cookie de refresh token");
        cookies!.Should().Contain(c => c.Contains("refresh_token", StringComparison.OrdinalIgnoreCase),
            "la cookie debe llamarse 'refresh_token'");
    }

    [Fact]
    public async Task PostLogin_WithInvalidCredentials_Returns401WithErrorContract()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "invalid@example.com", IsActive = true };
        await SeedUserAsync(user, "RealPass123!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { user.Email, Password = "WrongPassword" });

        // Assert — código de estado
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Assert — contrato de error: debe incluir errorCode y no exponer datos internos
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        root.GetProperty("isSuccess").GetBoolean()
            .Should().BeFalse("en error el campo isSuccess debe ser false");
        root.GetProperty("errorCode").GetString()
            .Should().Be("AUTH_INVALID_CREDENTIALS",
                "el código de error genérico previene la enumeración de cuentas");

        content.Should().NotContain("passwordHash");
        content.Should().NotContain("PasswordHash");
    }

    [Fact]
    public async Task PostLogin_WithLockedAccount_Returns423()
    {
        // Arrange
        var password = "Test@123!";
        var user = new User
        {
            Id          = Guid.NewGuid(),
            Email       = "locked@example.com",
            IsActive    = true,
            LockedUntil = DateTime.UtcNow.AddMinutes(10)
        };
        await SeedUserAsync(user, password);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { user.Email, Password = password });

        // Assert
        response.StatusCode.Should().Be((HttpStatusCode)423);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("errorCode").GetString()
            .Should().Be("ACCOUNT_LOCKED");
    }

    [Fact]
    public async Task PostLogout_WithValidToken_Returns204AndClearsCookie()
    {
        // Arrange
        var password = "Test@123!";
        var user = new User { Id = Guid.NewGuid(), Email = "logout@example.com", IsActive = true };
        await SeedUserAsync(user, password);

        var loginResp    = await _client.PostAsJsonAsync("/api/v1/auth/login", new { user.Email, Password = password });
        var loginContent = await loginResp.Content.ReadAsStringAsync();
        using var loginDoc = JsonDocument.Parse(loginContent);
        var token = ExtractToken(loginDoc);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        // Assert — 204 sin body
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().BeEmpty("logout no devuelve body");
    }

    [Fact]
    public async Task PostRefresh_WithoutCookie_Returns401WithErrorContract()
    {
        // Act
        var response = await _client.PostAsync("/api/v1/auth/refresh", null);

        // Assert — código de estado
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Assert — contrato de error
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("errorCode").GetString()
            .Should().Be("TOKEN_MISSING");
    }
}
