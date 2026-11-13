using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Eksen.Telemetry.AspNetCore.Tests;

/// <summary>
/// Records every log event with its formatted message, its structured state and the fields of every active scope.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public ConcurrentQueue<CapturedLogEvent> Events { get; } = new();

    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(this, categoryName);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public object? Property(string name)
    {
        foreach (var logEvent in Events)
        {
            if (logEvent.Properties.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        return null;
    }

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return provider._scopeProvider.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = new Dictionary<string, object?>(StringComparer.Ordinal);

            provider._scopeProvider.ForEachScope(static (scope, target) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
                {
                    foreach (var (key, value) in pairs)
                    {
                        target[key] = value;
                    }
                }
            }, properties);

            if (state is IEnumerable<KeyValuePair<string, object?>> statePairs)
            {
                foreach (var (key, value) in statePairs)
                {
                    properties[key] = value;
                }
            }

            provider.Events.Enqueue(new CapturedLogEvent(categoryName, logLevel, formatter(state, exception), exception, properties));
        }
    }
}

public sealed record CapturedLogEvent(
    string Category,
    LogLevel Level,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties);
