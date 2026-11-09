using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>Captures every log entry (all categories, all levels) including exception text and scopes.</summary>
public sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyList<string> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _entries);

    public void Dispose() { }

    private sealed class Logger(string category, ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            entries.Enqueue($"[scope] {category}: {state}");
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // Formatted message + every structured value + full exception text.
            var values = state is IEnumerable<KeyValuePair<string, object?>> kv
                ? string.Join(" ", kv.Select(p => $"{p.Key}={p.Value}"))
                : string.Empty;
            entries.Enqueue($"[{logLevel}] {category}: {formatter(state, exception)} | {values} | {exception}");
        }
    }
}
