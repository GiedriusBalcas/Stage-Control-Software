using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_control_software_WPF.view_models.logging
{
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;
        private readonly LogLevel _minLevel;
        private FileLogger _fileLogger;

        public FileLoggerProvider(string filePath, LogLevel minLevel)
        {
            _filePath = filePath;
            _minLevel = minLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            // We don't distinguish by categoryName here,
            // but you could if you want separate log files per category.
            return new FileLogger(categoryName, _filePath, _minLevel); ;
        }

        public void Dispose()
        {
            // On disposal, flush any remaining logs
            _fileLogger.Flush();
        }
    }
}
