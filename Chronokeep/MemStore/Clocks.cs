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

using Chronokeep.Helpers;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;

namespace Chronokeep.MemStore
{
    internal partial class MemStore
    {
        /**
         * Banned Email/Phone Functions
         */

        public List<Chronoclock> GetClocks()
        {
            Log.D("MemStore", "GetClocks");
            List<Chronoclock> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output.AddRange(clocks.Values);
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
            return output;
        }

        public int AddClock(Chronoclock clock)
        {
            Log.D("MemStore", "AddClock");
            clock.Identifier = database.AddClock(clock);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return clock.Identifier;
                try
                {
                    clocks[clock.Identifier] = clock;
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
            return clock.Identifier;
        }

        public void UpdateClock(Chronoclock clock)
        {
            Log.D("MemStore", "ClearBannedEmails");
            database.UpdateClock(clock);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    clocks[clock.Identifier] = clock;
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public void RemoveClocks(List<Chronoclock> iClocks)
        {
            Log.D("MemStore", "RemoveBannedPhones");
            database.RemoveClocks(iClocks);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    foreach (Chronoclock clock in iClocks)
                    {
                        clocks.Remove(clock.Identifier);
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }
    }
}
