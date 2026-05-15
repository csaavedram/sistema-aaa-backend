using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using SistemaAAA.API.Middleware;
using SistemaAAA.Domain.Interfaces;
using Xunit;

namespace SistemaAAA.Tests.Unit.Infrastructure;

/// <summary>
/// Tests unitarios para JwtMiddleware.
/// Verifica la intercepción de requests, la validación de tokens y el manejo seguro de fallos.
/// </summary>
public class JwtMiddlewareTests
{
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<ILogger<JwtMiddleware>> _mockLogger;
    private readonly JwtMiddleware _middleware;

    public JwtMiddlewareTests()
    {
        _mockJwtService = new Mock<IJwtService>();
        _mockLogger = new Mock<ILogger<JwtMiddleware>>();

        // Inicializamos el middleware con sus dependencias inyectadas (Patrón IMiddleware)
        _middleware = new JwtMiddleware(_mockJwtService.Object, _mockLogger.Object);
    }

    /// <summary>
    /// Helper para simular el HttpContext de una petición web.
    /// </summary>
    private HttpContext CreateContext(string? authHeader = null, string path = "/api/v1/users")
    {
        var context = new DefaultHttpContext();
        if (authHeader != null)
        {
            context.Request.Headers.Authorization = authHeader;
        }
        context.Request.Path = path;
        return context;
    }

    [Fact]
    public async Task InvokeAsync_WithValidToken_EnrichesHttpContextUser()
    {
        // Arrange
        var token = "valid.jwt.token";
        var context = CreateContext($"Bearer {token}");
        RequestDelegate next = (HttpContext hc) => Task.CompletedTask; // El delegado "siguiente"
        
        var userId = Guid.NewGuid();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));

        _mockJwtService
            .Setup(x => x.ValidateToken(token, out principal))
            .Returns(true);

        // Act
        // Pasamos el contexto y el RequestDelegate a InvokeAsync
        await _middleware.InvokeAsync(context, next);

        // Assert
        context.User.Should().NotBeNull();
        context.User.Identity?.IsAuthenticated.Should().BeTrue();
        context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be(userId.ToString());
        
        _mockJwtService.Verify(x => x.ValidateToken(token, out It.Ref<ClaimsPrincipal?>.IsAny), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidToken_ContinuesWithoutEnriching()
    {
        // Arrange
        var token = "invalid.jwt.token";
        var context = CreateContext($"Bearer {token}");
        RequestDelegate next = (HttpContext hc) => Task.CompletedTask;
        ClaimsPrincipal? principal = null;

        _mockJwtService
            .Setup(x => x.ValidateToken(token, out principal))
            .Returns(false);

        var originalUser = context.User;

        // Act
        await _middleware.InvokeAsync(context, next);

        // Assert
        context.User.Should().Be(originalUser); // El usuario no fue modificado
        context.User.Identity?.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WithExcludedPath_SkipsValidation()
    {
        // Arrange
        var context = CreateContext("Bearer some.token", "/api/v1/auth/login");
        RequestDelegate next = (HttpContext hc) => Task.CompletedTask;

        // Act
        await _middleware.InvokeAsync(context, next);

        // Assert
        _mockJwtService.Verify(x => x.ValidateToken(It.IsAny<string>(), out It.Ref<ClaimsPrincipal?>.IsAny), Times.Never);
        context.User.Identity?.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WithMalformedToken_ContinuesSafely()
    {
        // Arrange
        var token = "not.a.valid.jwt.token.structure";
        var context = CreateContext($"Bearer {token}");
        RequestDelegate next = (HttpContext hc) => Task.CompletedTask;
        ClaimsPrincipal? principal = null;

        _mockJwtService
            .Setup(x => x.ValidateToken(token, out principal))
            .Throws(new Exception("Internal token validation error"));

        // Act
        Func<Task> act = async () => await _middleware.InvokeAsync(context, next);

        // Assert
        // CRÍTICO: el middleware nunca debe bloquear un request por error interno
        await act.Should().NotThrowAsync<Exception>("el middleware debe capturar la excepción internamente y permitir que next() continúe");
        context.User.Identity?.IsAuthenticated.Should().BeFalse(); // No autenticado, pero continuó el flujo
    }
}