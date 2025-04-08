using System;
using System.IO;

namespace Genymobile.Gnirehtet.Relay
{
    public static class Log
    {
        public enum Level
        {
            VERBOSE,
            DEBUG,
            INFO,
            WARNING,
            ERROR
        }

        private static Level threshold = Level.INFO;
        private static readonly string DateFormat = "yyyy-MM-dd HH:mm:ss.fff";
        private static readonly DateTime CurrentDate = DateTime.Now;

        public static Level Threshold
        {
            get { return threshold; }
            set { threshold = value; }
        }

        public static bool IsEnabled(Level level)
        {
            return level >= threshold;
        }

        public static bool IsVerboseEnabled()
        {
            return IsEnabled(Level.VERBOSE);
        }

        public static bool IsDebugEnabled()
        {
            return IsEnabled(Level.DEBUG);
        }

        public static bool IsInfoEnabled()
        {
            return IsEnabled(Level.INFO);
        }

        public static bool IsWarningEnabled()
        {
            return IsEnabled(Level.WARNING);
        }

        public static bool IsErrorEnabled()
        {
            return IsEnabled(Level.ERROR);
        }

        private static string GetDate()
        {
            return CurrentDate.ToString(DateFormat);
        }

        private static string Format(Level level, string tag, string message)
        {
            return $"{GetDate()} {level} {tag}: {message}";
        }

        private static void LogMessage(Level level, TextWriter stream, string tag, string message, Exception e)
        {
            if (IsEnabled(level))
            {
                stream.WriteLine(Format(level, tag, message));
                if (e != null)
                {
                    stream.WriteLine(e.ToString());
                }
            }
        }

        public static void V(string tag, string message, Exception e = null)
        {
            LogMessage(Level.VERBOSE, Console.Out, tag, message, e);
        }

        public static void D(string tag, string message, Exception e = null)
        {
            LogMessage(Level.DEBUG, Console.Out, tag, message, e);
        }

        public static void I(string tag, string message, Exception e = null)
        {
            LogMessage(Level.INFO, Console.Out, tag, message, e);
        }

        public static void W(string tag, string message, Exception e = null)
        {
            LogMessage(Level.WARNING, Console.Out, tag, message, e);
        }

        public static void E(string tag, string message, Exception e = null)
        {
            LogMessage(Level.ERROR, Console.Error, tag, message, e);
        }
    }
}
