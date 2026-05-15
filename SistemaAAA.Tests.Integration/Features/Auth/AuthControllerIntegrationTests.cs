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
                ["Jwt:SecretKey"] = "test-secret-key-at-least-32-characters!!",
                ["Jwt:Issuer"] = "SistemaAAA-Test",
                ["Jwt:Audience"] = "SistemaAAA-clients-Test",
                ["Jwt:ExpiresMinutes"] = "60",
                ["Seed:RunOnStartup"] = "false"
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
        // CRÍTICO: Generar el nombre de la BD AFUERA para que sea el mismo en toda la ejecución del test
        var dbName = "TestDb_" + Guid.NewGuid().ToString();

        _factory = factory.WithWebHostBuilder(builder => 
        {
            builder.ConfigureServices(services => 
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                
                // Ahora todos usan el mismo dbName en memoria
                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(dbName));
            });
        });
        
        _client = _factory.CreateClient();
    }

    private async Task SeedUserAsync(User user, string clearPassword)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        
        user.PasswordHash = hasher.Hash(clearPassword);
        
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task PostLogin_WithValidCredentials_Returns200WithAccessTokenAndSetsCookie()
    {
        var password = "Test@123!";
        var user = new User { Id = Guid.NewGuid(), Email = "validuser@example.com", IsActive = true };
        await SeedUserAsync(user, password);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { user.Email, Password = password });

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Respuesta: {content}");
        
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        string? token = null;
        if (root.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null) token = v.GetProperty("accessToken").GetString();
        else if (root.TryGetProperty("data", out var d) && d.ValueKind != JsonValueKind.Null) token = d.GetProperty("accessToken").GetString();
        else token = root.GetProperty("accessToken").GetString();

        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostLogin_WithInvalidCredentials_Returns401WithGenericError()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "invalid@example.com", IsActive = true };
        await SeedUserAsync(user, "RealPass123!");

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { user.Email, Password = "WrongPassword" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostLogin_WithLockedAccount_Returns423()
    {
        var password = "Test@123!";
        var user = new User { Id = Guid.NewGuid(), Email = "locked@example.com", IsActive = true, LockedUntil = DateTime.UtcNow.AddMinutes(10) };
        await SeedUserAsync(user, password);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { user.Email, Password = password });

        response.StatusCode.Should().Be((HttpStatusCode)423);
    }

    [Fact]
    public async Task PostLogout_WithValidToken_Returns204AndClearsCookie()
    {
        var password = "Test@123!";
        var user = new User { Id = Guid.NewGuid(), Email = "logout@example.com", IsActive = true };
        await SeedUserAsync(user, password);

        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { user.Email, Password = password });
        var loginContent = await loginResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(loginContent);
        
        string? token = null;
        if (doc.RootElement.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null) token = v.GetProperty("accessToken").GetString();
        else if (doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind != JsonValueKind.Null) token = d.GetProperty("accessToken").GetString();
        else token = doc.RootElement.GetProperty("accessToken").GetString();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PostRefresh_WithoutCookie_Returns401()
    {
        var response = await _client.PostAsync("/api/v1/auth/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}