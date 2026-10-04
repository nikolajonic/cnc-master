using Serilog.Core;
using Serilog.Events;

namespace CNC.Infrastructure.Logging;

internal sealed class ThreadIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("ThreadId", Environment.CurrentManagedThreadId));
    }
}
