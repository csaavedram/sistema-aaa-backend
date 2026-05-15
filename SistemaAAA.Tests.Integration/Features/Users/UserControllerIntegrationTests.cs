using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaAAA.API;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;
using SistemaAAA.Infrastructure.Persistence;
using SistemaAAA.Tests.Integration.Auth;
using Xunit;

namespace SistemaAAA.Tests.Integration.Users;

public class UsersControllerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public UsersControllerIntegrationTests(TestWebApplicationFactory factory)
    {
        var dbName = "TestDbUsers_" + Guid.NewGuid().ToString();

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

    private async Task<(User User, string Token)> SeedAdminAndLoginAsync()
    {
        var password = "Admin@123!";
        var admin = new User { Id = Guid.NewGuid(), Email = "admin@example.com", IsActive = true };

        using (var scope = _factory.Services.CreateScope())
        {
            var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            admin.PasswordHash = hasher.Hash(password);

            db.Users.Add(admin);

            var role       = new Role       { Id = Guid.NewGuid(), Name = "Admin" };
            var readPerm   = new Permission { Id = Guid.NewGuid(), Name = "users.read" };
            var createPerm = new Permission { Id = Guid.NewGuid(), Name = "users.create" };
            db.Roles.Add(role);
            db.Permissions.AddRange(readPerm, createPerm);
            db.Set<UserRole>().Add(new UserRole { UserId = admin.Id, RoleId = role.Id });
            db.Set<RolePermission>().Add(new RolePermission { RoleId = role.Id, PermissionId = readPerm.Id });
            db.Set<RolePermission>().Add(new RolePermission { RoleId = role.Id, PermissionId = createPerm.Id });

            await db.SaveChangesAsync();
        }

        var loginResp    = await _client.PostAsJsonAsync("/api/v1/auth/login", new { Email = admin.Email, Password = password });
        var loginContent = await loginResp.Content.ReadAsStringAsync();
        using var doc    = JsonDocument.Parse(loginContent);
        var root         = doc.RootElement;

        string? token = null;
        if (root.TryGetProperty("data", out var d) && d.ValueKind != JsonValueKind.Null)
            token = d.GetProperty("accessToken").GetString();
        else
            token = root.GetProperty("accessToken").GetString();

        return (admin, token!);
    }

    // ─── Tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUsers_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/users");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUsers_WithAdminToken_Returns200WithUserListContract()
    {
        // Arrange
        var setup   = await SeedAdminAndLoginAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", setup.Token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — código de estado
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — contrato exacto: { success, data: { users[], page, pageSize } }
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeTrue();

        var data = root.GetProperty("data");
        data.GetProperty("users").ValueKind
            .Should().Be(JsonValueKind.Array, "users debe ser un array");
        data.GetProperty("page").GetInt32()
            .Should().BeGreaterThan(0);
        data.GetProperty("pageSize").GetInt32()
            .Should().BeGreaterThan(0);

        // Cada usuario del array expone las propiedades esperadas y no expone campos sensibles
        foreach (var userElement in data.GetProperty("users").EnumerateArray())
        {
            userElement.TryGetProperty("id",        out _).Should().BeTrue("falta la propiedad 'id'");
            userElement.TryGetProperty("email",     out _).Should().BeTrue("falta la propiedad 'email'");
            userElement.TryGetProperty("isActive",  out _).Should().BeTrue("falta la propiedad 'isActive'");
            userElement.TryGetProperty("createdAt", out _).Should().BeTrue("falta la propiedad 'createdAt'");
        }

        // Sin campos sensibles en todo el payload
        content.Should().NotContain("passwordHash", "el hash de contraseña nunca debe exponerse");
        content.Should().NotContain("PasswordHash", "el hash de contraseña nunca debe exponerse (PascalCase)");
    }

    [Fact]
    public async Task PostUser_WithAdminToken_Returns201WithUserContract()
    {
        // Arrange
        var setup   = await SeedAdminAndLoginAsync();
        var newUser = new { Email = "new_integration@example.com", Password = "StrongPassword123!" };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users")
        {
            Content = JsonContent.Create(newUser)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", setup.Token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — código de estado y Location header
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull("debe devolver un header Location con la URL del recurso");

        // Assert — contrato exacto: { success, data: { userId, email, createdAt } }
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeTrue();

        var data = root.GetProperty("data");
        data.GetProperty("userId").GetString()
            .Should().NotBeNullOrEmpty("se debe devolver el userId del nuevo usuario");
        data.GetProperty("email").GetString()
            .Should().Be(newUser.Email, "el email debe coincidir con el solicitado");
        data.GetProperty("createdAt").ValueKind
            .Should().Be(JsonValueKind.String, "createdAt debe ser una fecha en formato string");

        // Sin campos sensibles — ni texto plano ni hash de contraseña
        content.Should().NotContain("passwordHash", "el hash de contraseña nunca debe exponerse");
        content.Should().NotContain("PasswordHash", "el hash de contraseña nunca debe exponerse (PascalCase)");
        content.Should().NotContain("password",     "la contraseña en texto plano nunca debe devolverse");
    }
}
