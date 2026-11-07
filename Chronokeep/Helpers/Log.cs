/*
Chronokeep Desktop - Race Scoring Software
Copyright (C) 2026 James Sentinella

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Chronokeep.Helpers
{
    internal static class Log
    {
        private const bool OutputDebug = true;
        private static readonly Lock FileLock = new();

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
        /// <summary>
        /// Writes an error to the error log. Unlike the other log functions this also runs in release builds.
        /// </summary>
        /// <param name="ns">Where the error happened, usually the namespace and class.</param>
        /// <param name="msg">The error.</param>
        public static void E(string ns, string msg)
        {
            Debug.WriteLine($"{DateTime.Now:hh:mm:ss.fff} LOGOUTPUT - e - {ns} - {msg}");
            if (Globals.ErrorLogPath.Length < 1)
            {
                return;
            }
            // Errors are logged from several threads, so only one writes at a time. A failure to write
            // the log shouldn't take down whatever was logging the error.
            lock (FileLock)
            {
                try
                {
                    bool newFile = !File.Exists(Globals.ErrorLogPath);
                    File.AppendAllText(Globals.ErrorLogPath, $"{DateTime.Now:hh:mm:ss.fff}: {ns[..(ns.Length > 20 ? 20 : ns.Length)],-20} - {msg}\n");
                    // Each day's log is shared by every user on the computer, so whoever starts it lets everyone write to it.
                    if (newFile)
                    {
                        ProgramFolders.ShareFile(Globals.ErrorLogPath);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"{DateTime.Now:hh:mm:ss.fff} LOGOUTPUT - e - Helpers.Log - Unable to write to the error log. {e.Message}");
                }
            }
        }
    }
}

