using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace MunitionAutoPatcher.Logging
{
    /// <summary>
    /// ファイルにログを出力する ILoggerProvider
    /// UI スレッドに影響しないため、大量ログでも UI フリーズしない
    /// </summary>
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;
        private readonly object _lock = new object();

        public FileLoggerProvider(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            
            // ディレクトリ作成
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // ファイルが既に存在する場合はクリア（新規セッション）
            try
            {
                if (File.Exists(_filePath))
                {
                    File.WriteAllText(_filePath, $"=== New Session Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
                }
            }
            catch
            {
                // ファイルクリア失敗は無視
            }
        }

        public ILogger CreateLogger(string categoryName) 
            => new FileLogger(categoryName, _filePath, _lock);

        public void Dispose() { }
    }

    internal class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly string _filePath;
        private readonly object _lock;

        public FileLogger(string category, string filePath, object lockObj)
        {
            _category = category;
            _filePath = filePath;
            _lock = lockObj;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull 
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter == null) return;

            try
            {
                var message = formatter(state, exception);
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var formatted = $"[{timestamp}] [{logLevel}] [{_category}] {message}";

                if (exception != null)
                {
                    formatted += $"\n{exception}";
                }

                // ファイルロックして書き込み（UI スレッドに影響しない）
                lock (_lock)
                {
                    File.AppendAllText(_filePath, formatted + "\n");
                }
            }
            catch
            {
                // ファイル書き込み失敗は無視（UI に影響させない）
            }
        }

        private class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose() { }
        }
    }
}
