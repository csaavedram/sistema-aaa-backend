using Microsoft.Extensions.Logging;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.API.Services;

public class StubEmailService : IEmailService
{
    private readonly ILogger<StubEmailService> _logger;

    public StubEmailService(ILogger<StubEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string resetLink, CancellationToken ct = default)
    {
        _logger.LogInformation("Password reset link: {ResetLink}", resetLink);
        return Task.CompletedTask;
    }
}