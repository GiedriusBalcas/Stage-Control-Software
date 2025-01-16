using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_control_software_WPF.view_models.logging
{
    public static class OnDemandFileLoggerExtensions
    {
        public static ILoggingBuilder AddOnDemandFileLogger(
            this ILoggingBuilder builder,
            string filePath,
            LogLevel minLevel
        )
        {
            builder.AddProvider(new FileLoggerProvider(filePath, minLevel));
            return builder;
        }
    }
}
