using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;
using SistemaAAA.Infrastructure.Persistence;
using SistemaAAA.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SistemaAAA.Tests.Unit.Infrastructure;

/// <summary>
/// Tests de infraestructura para AuditRepository.
/// Utiliza EF Core InMemory para garantizar que la persistencia y los filtros
/// operan correctamente sobre el DbContext real simulado.
/// </summary>
public class AuditRepositoryTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task InsertAsync_PersistsAuditLog_AndSavesChanges()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new AuditRepository(context);

        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventType = "TEST_EVENT",
            Resource = "Users", // Corregido: antes TableName
            CreatedAt = DateTime.UtcNow, // Corregido: antes Timestamp
            IpAddress = "127.0.0.1",
            Details = "Test audit"
        };

        // Act
        await repo.InsertAsync(log, CancellationToken.None);

        // Assert
        // CRÍTICO: verificar que SaveChangesAsync fue efectivamente llamado
        var savedLog = await context.AuditLogs.FirstOrDefaultAsync(x => x.Id == log.Id);
        
        context.AuditLogs.Count().Should().Be(1);
        savedLog.Should().NotBeNull();
        savedLog!.EventType.Should().Be("TEST_EVENT");
        savedLog.IpAddress.Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task InsertAsync_WithNullLog_ThrowsArgumentNullException()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new AuditRepository(context);

        // Act
        Func<Task> act = async () => await repo.InsertAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetLogsAsync_WithEventTypeFilter_ReturnsOnlyMatchingLogs()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new AuditRepository(context);

        await context.AuditLogs.AddRangeAsync(new List<AuditLog>
        {
            new() { Id = Guid.NewGuid(), EventType = "LOGIN_SUCCESS", Resource = "Auth", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), EventType = "LOGIN_SUCCESS", Resource = "Auth", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), EventType = "LOGIN_FAILURE", Resource = "Auth", CreatedAt = DateTime.UtcNow }
        });
        await context.SaveChangesAsync();

        var filter = new AuditLogFilter { EventType = "LOGIN_SUCCESS" };

        // Act
        var result = await repo.GetLogsAsync(filter, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.All(x => x.EventType == "LOGIN_SUCCESS").Should().BeTrue();
    }

    [Fact]
    public async Task GetLogsAsync_WithDateRangeFilter_ReturnsLogsInRange()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new AuditRepository(context);

        var now = DateTime.UtcNow;

        await context.AuditLogs.AddRangeAsync(new List<AuditLog>
        {
            new() { Id = Guid.NewGuid(), EventType = "EV_1", Resource = "Auth", CreatedAt = now.AddDays(-5) }, // Fuera
            new() { Id = Guid.NewGuid(), EventType = "EV_2", Resource = "Auth", CreatedAt = now },             // Dentro
            new() { Id = Guid.NewGuid(), EventType = "EV_3", Resource = "Auth", CreatedAt = now.AddDays(5) }   // Fuera
        });
        await context.SaveChangesAsync();

        var filter = new AuditLogFilter 
        { 
            From = now.AddDays(-1), 
            To = now.AddDays(1) 
        };

        // Act
        var result = await repo.GetLogsAsync(filter, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().EventType.Should().Be("EV_2");
    }

    [Fact]
    public void AuditLog_IsImmutable_NoUpdateOrDeleteMethods()
    {
        // Arrange
        var repositoryType = typeof(IAuditRepository);
        var methods = repositoryType.GetMethods().Select(m => m.Name.ToLowerInvariant());

        // Act & Assert
        // CRÍTICO: la inmutabilidad de AuditLog es un requisito legal
        methods.Should().NotContain(name => name.Contains("update"));
        methods.Should().NotContain(name => name.Contains("delete"));
        methods.Should().NotContain(name => name.Contains("remove"));
        methods.Should().NotContain(name => name.Contains("modify"));
    }
}