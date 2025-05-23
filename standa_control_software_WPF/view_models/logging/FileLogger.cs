﻿using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace standa_control_software_WPF.view_models.logging
{
    public class FileLogger : ILogger, IFlushableLogger
    {
        private readonly string _categoryName;
        private readonly string _filePath;
        private readonly LogLevel _minLevel;
        private readonly ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        private readonly object _fileLock = new object();

        public FileLogger(string categoryName, string filePath, LogLevel minLevel)
        {
            if (!string.IsNullOrEmpty(categoryName))
            {
                var lastDotIndex = categoryName.LastIndexOf('.');
                if (lastDotIndex >= 0 && lastDotIndex < categoryName.Length - 1)
                {
                    categoryName = categoryName.Substring(lastDotIndex + 1);
                }
            }

            _categoryName = categoryName;
            _filePath = filePath;
            _minLevel = minLevel;
        }

        /// <summary>
        /// Begins a logical operation scope. Not implemented.
        /// </summary>
        IDisposable ILogger.BeginScope<TState>(TState state) => NullDisposable.Instance;


        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minLevel;
        }
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (!IsEnabled(logLevel)) return;
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));

            var message = formatter(state, exception);
            var logRecord = $"{DateTime.Now:HH:mm:ss.fff} [{logLevel}][{_categoryName}] {message}";
            if (exception != null)
            {
                logRecord += Environment.NewLine + exception;
            }

            // Enqueue the record; we won't write it until Flush() is called
            _logQueue.Enqueue(logRecord);
            Flush();
        }
        /// <summary>
        /// Writes all queued logs to file, then clears the queue.
        /// </summary>
        public void Flush()
        {
            if (_logQueue.IsEmpty) return;

            // Gather all queued messages into one big string
            var sb = new StringBuilder();
            while (_logQueue.TryDequeue(out var line))
            {
                sb.AppendLine(line);
            }

            lock (_fileLock)
            {
                try
                {
                    // Attempt to append the entire buffer to the file
                    File.AppendAllText(_filePath, sb.ToString());
                }
                catch
                {
                    // If writing fails, log that fact and re-queue the entire batch
                    sb.AppendLine("failed to save log file.");
                    _logQueue.Enqueue(sb.ToString());
                }
            }
        }


        private class NullDisposable : IDisposable
        {
            // Singleton instance to avoid multiple allocations
            public static readonly NullDisposable Instance = new NullDisposable();

            // Private constructor to prevent external instantiation
            private NullDisposable() { }

            // Dispose method does nothing
            public void Dispose() { }
        }

    }


}
