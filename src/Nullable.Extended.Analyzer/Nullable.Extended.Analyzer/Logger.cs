using System;
using System.Diagnostics;
using System.IO;

namespace Nullable.Extended.Analyzer
{
    internal static class Logger
    {
        public static string LogFile = @"c:\Temp\NullableExtendedAnalyzer.log";

        [Conditional("DEBUG")]
        public static void Log(Func<string> getMessage)
        {
            Log(getMessage());
        }

        [Conditional("DEBUG")]
        public static void Log(string message)
        {
            try
            {
                lock (typeof(Logger))
                {
                    File.AppendAllText(LogFile, message + Environment.NewLine);
                }
            }
            catch
            {
                // ignore IO errors...
            }
        }
    }
}
