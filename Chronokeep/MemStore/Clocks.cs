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