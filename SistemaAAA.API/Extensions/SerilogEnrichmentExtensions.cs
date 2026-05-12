using Serilog;
using Serilog.Configuration;

namespace SistemaAAA.API.Extensions;

public static class SerilogEnrichmentExtensions
{
    public static LoggerConfiguration WithMachineName(this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        return enrichmentConfiguration.WithProperty("MachineName", Environment.MachineName);
    }

    public static LoggerConfiguration WithThreadId(this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        return enrichmentConfiguration.WithProperty("ThreadId", Environment.CurrentManagedThreadId);
    }
}