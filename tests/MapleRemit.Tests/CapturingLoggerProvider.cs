using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MapleRemit.Tests;

public sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);

// Captures logs for assertions.
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public IEnumerable<LogEntry> Errors => Entries.Where(e => e.Level >= LogLevel.Error);

    public IEnumerable<LogEntry> Infos => Entries.Where(e => e.Level == LogLevel.Information);

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

    public void Dispose() { }

    private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception));
    }
}
