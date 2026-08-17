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

