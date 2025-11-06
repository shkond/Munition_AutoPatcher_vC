using System;
using Microsoft.Extensions.Logging;

namespace MunitionAutoPatcher.Logging
{
    // ILogger provider that forwards all ILogger events into the legacy AppLogger
    public class AppLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new AppLoggerWrapper(categoryName);

        public void Dispose() { }
    }

    internal class AppLoggerWrapper : ILogger
    {
        private readonly string _category;

        public AppLoggerWrapper(string category) => _category = category;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter == null) return;
            try
            {
                var message = formatter(state, exception);
                var formatted = $"[{logLevel}] [{_category}] {message}";
                AppLogger.Log(formatted, exception);
            }
            catch (Exception ex)
            {
                try { AppLogger.Log($"AppLoggerWrapper: failed to format log: {ex.Message}", ex); } catch { }
            }
        }

        private class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose() { }
        }
    }
}
