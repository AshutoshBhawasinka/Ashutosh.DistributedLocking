using Ashutosh.Common.Logger;
using Ashutosh.DistributedLocking.Service.Controllers;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Ashutosh.DistributedLocking.Service.Logging
{
    public class CustomLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new CustomLogger();
        }

        public void Dispose()
        {
        }
    }

    public class CustomLogger : ILogger
    {
        private readonly Logger _logger;

        public CustomLogger()
        {
            _logger = new Logger("LockingService", typeof(Program).Namespace);
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message = formatter(state, exception);

            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    _logger.LogVerbose(message);
                    break;
                case LogLevel.Information:
                    _logger.Log(message);
                    break;
                case LogLevel.Warning:
                    if (exception != null)
                    {
                        _logger.LogWarning(exception, message);
                    }
                    else
                    {
                        _logger.LogWarning(message);
                    }
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    if (exception != null)
                    {
                        _logger.LogError(exception, message);
                    }
                    else
                    {
                        _logger.LogError(message);
                    }
                    break;
                default:
                    _logger.Log(message);
                    break;
            }
        }
    }
}
