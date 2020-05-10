using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;

namespace MotionMonitor
{
    internal enum Level
    {
        Info,
        Warning,
        Debug,
        Error
    }
    internal static class Logger
    {
        private static readonly bool _enableDebug = ConfigurationManager.AppSettings["debug"].Equals("true", StringComparison.OrdinalIgnoreCase);
        private static Object _lck = new object();
        private static string _appFileName = System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".log";

        internal static void Error(string message, bool writeToConsole = true)
        {
            Message(message, Level.Error, writeToConsole);
        }

        internal static void Info(string message, bool writeToConsole = true)
        {
            Message(message, Level.Info, writeToConsole);
        }

        internal static void Warning(string message, bool writeToConsole = true)
        {
            Message(message, Level.Warning, writeToConsole);
        }

        internal static void Debug(string message, bool writeToConsole = true)
        {
            if (_enableDebug)
            {
                Message(message, Level.Debug, writeToConsole);
            }
        }

        internal static void Message(string message, Level level, bool writeToConsole = true)
        {
            lock (_lck)
            {
                FileStream fs = null;
                StreamWriter w = null;
                try
                {
                    fs = new FileStream(_appFileName, FileMode.Append);
                    w = new StreamWriter(fs);
                    string modifiedMessage = $"[{level.ToString()}]: {message}";
                    w.WriteLine(modifiedMessage);
                    if (writeToConsole)
                    {
                        Console.WriteLine(modifiedMessage);
                    }
                }
                finally
                {
                    w?.Close();
                    fs?.Close();
                }
            }
        }
    }
}
