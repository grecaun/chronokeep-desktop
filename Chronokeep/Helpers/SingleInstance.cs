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
using System.Collections.Generic;
using System.Threading;

namespace Chronokeep.Helpers
{
    /// <summary>
    /// Makes sure only one copy of Chronokeep runs at a time. The app claims this once at startup,
    /// before any window is made, and holds it until it exits. A debug build and a release build use
    /// different names, so one of each can run side by side.
    /// </summary>
    internal static class SingleInstance
    {
#if DEBUG
        public const string Name = "{48ED48DE-6E1B-4F3B-8C5C-D0BAB5295366}-chronokeep-debug";
#else
        public const string Name = "{48ED48DE-6E1B-4F3B-8C5C-D0BAB5295366}-chronokeep";
#endif

        // Held for the life of the program; letting one be collected would release the claim.
        private static readonly Dictionary<string, Mutex> Held = [];
        private static readonly Lock HeldLock = new();

        /// <summary>True if this is the only copy running (or already claimed it); false if another copy has it.</summary>
        public static bool Claim() => Claim(Name);

        internal static bool Claim(string name)
        {
            lock (HeldLock)
            {
                if (Held.ContainsKey(name)) return true;
                Mutex mutex = new(true, name, out bool createdNew);
                bool owned = createdNew;
                if (!owned)
                {
                    try
                    {
                        owned = mutex.WaitOne(TimeSpan.Zero);
                    }
                    catch (AbandonedMutexException)
                    {
                        // The copy that held it ended without letting go, so it's ours now.
                        owned = true;
                    }
                }
                if (!owned)
                {
                    mutex.Dispose();
                    return false;
                }
                Held[name] = mutex;
                return true;
            }
        }
    }
}
