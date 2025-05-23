﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace standa_control_software_WPF.view_models.logging
{
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;
        private readonly LogLevel _minLevel;
        private readonly ConcurrentBag<FileLogger> _loggers = new ConcurrentBag<FileLogger>();
        private bool _disposed = false;

        public FileLoggerProvider(string filePath, LogLevel minLevel, bool clearOnLaunch = true)
        {
            _filePath = filePath;
            _minLevel = minLevel;

            if (clearOnLaunch && File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            var logger = new FileLogger(categoryName, _filePath, _minLevel);
            _loggers.Add(logger);
            return logger;
        }

        public void Dispose()
        {
            if (_disposed) return;

            foreach (var logger in _loggers)
            {
                logger.Flush();
            }

            _disposed = true;
        }
    }
}
