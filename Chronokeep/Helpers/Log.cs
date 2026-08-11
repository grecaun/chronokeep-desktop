using System;
using System.Diagnostics;
using System.IO;

namespace Chronokeep.Helpers
{
    internal static class Log
    {
        private const bool OutputDebug = true;

        [Conditional("DEBUG")]
        public static void D(string ns, string msg)
        {
            if (OutputDebug)
            {
                Debug.WriteLine($"{DateTime.Now:hh:mm:ss.fff} LOGOUTPUT - d - {ns} - {msg}");
            }
        }
        [Conditional("DEBUG")]
        public static void F(string ns, string msg)
        {
            if (OutputDebug)
            {
                Debug.WriteLine($"{DateTime.Now:hh:mm:ss.fff} LOGOUTPUT - f - {ns} - {msg}");
            }
        }
        [Conditional("DEBUG")]
        public static void E(string ns, string msg)
        {
            Debug.WriteLine($"{DateTime.Now:hh:mm:ss.fff} LOGOUTPUT - e - {ns} - {msg}");
            File.AppendAllText(Globals.ErrorLogPath, $"{DateTime.Now:hh:mm:ss.fff}: {ns[..(ns.Length > 20 ? 20 : ns.Length)],-20} - {msg}\n");
        }
    }
}
