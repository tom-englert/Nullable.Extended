using System;
using System.Collections.Immutable;
using System.IO;

namespace Nullable.Extended.Analyzer
{
    internal class Logger
    {
        private static ImmutableDictionary<string, Logger> _loggers = ImmutableDictionary<string, Logger>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);

        private readonly string? _logFile;
        private readonly object _mutex = new object();


        private Logger(string? logFile)
        {
            _logFile = logFile;
        }

        public static Logger Get(string? logFile)
        {
            return ImmutableInterlocked.GetOrAdd(ref _loggers, logFile ?? string.Empty, file => new Logger(file));
        }

        public void Log(Func<string> getMessage)
        {
            if (string.IsNullOrEmpty(_logFile))
                return;

            Log(_logFile!, getMessage());
        }

        public void Log(string message)
        {
            if (string.IsNullOrEmpty(_logFile))
                return;

            Log(_logFile!, message);
        }

        private void Log(string logFile, string message)
        {
            try
            {
                lock (_mutex)
                {
                    File.AppendAllText(logFile, message + Environment.NewLine);
                }
            }
            catch
            {
                // ignore IO errors...
            }
        }
    }
}