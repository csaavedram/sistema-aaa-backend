using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Application.Features.Audit;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Tests.Unit.Features.Audit;

/// <summary>
/// Unit tests for audit functionality.
/// </summary>
public class AuditLogHandlerTests
{
    private readonly Mock<IAuditRepository> _mockAuditRepository;
    private readonly Mock<ILogger<SearchAuditLogsQueryHandler>> _mockLogger;
    private readonly SearchAuditLogsQueryHandler _handler;

    public AuditLogHandlerTests()
    {
        _mockAuditRepository = new Mock<IAuditRepository>();
        _mockLogger = new Mock<ILogger<SearchAuditLogsQueryHandler>>();

        _handler = new SearchAuditLogsQueryHandler(
            _mockAuditRepository.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidFilters_ReturnsAuditLogs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EventType = "LOGIN_SUCCESS",
                Resource = "Auth",
                Details = "User logged in successfully",
                IpAddress = "127.0.0.1",
                CreatedAt = DateTime.UtcNow
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EventType = "PASSWORD_CHANGED",
                Resource = "User",
                Details = "User changed their password",
                IpAddress = "127.0.0.1",
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockAuditRepository.Setup(x => x.GetLogsAsync(It.IsAny<AuditLogFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(auditLogs);

        var query = new SearchAuditLogsQuery(userId, "LOGIN_SUCCESS", "Auth", null, null, 1, 50);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Logs.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithDateRangeFilter_ReturnsFilteredLogs()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;

        var auditLogs = new List<AuditLog>
        {
            new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                EventType = "LOGIN_SUCCESS",
                Resource = "Auth",
                Details = null,
                IpAddress = "127.0.0.1",
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            }
        };

        _mockAuditRepository.Setup(x => x.GetLogsAsync(It.IsAny<AuditLogFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(auditLogs);

        var query = new SearchAuditLogsQuery(null, null, null, from, to, 1, 50);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Logs.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange
        var page = 2;
        var pageSize = 10;
        var auditLogs = new List<AuditLog>();

        _mockAuditRepository.Setup(x => x.GetLogsAsync(It.IsAny<AuditLogFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(auditLogs);

        var query = new SearchAuditLogsQuery(null, null, null, null, null, page, pageSize);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Page.Should().Be(page);
        result.Value!.PageSize.Should().Be(pageSize);
    }
}
