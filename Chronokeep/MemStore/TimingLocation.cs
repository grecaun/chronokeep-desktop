using Chronokeep.Helpers;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.MemStore
{
    internal partial class MemStore
    {
        /**
         * TimingLocation Functions
         */

        public int AddTimingLocation(TimingLocation tp)
        {
            Log.D("MemStore", "AddTimingLocation");
            int output = database.AddTimingLocation(tp);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    tp.Identifier = output;
                    if (theEvent != null && tp.EventIdentifier == theEvent.Identifier && tp.Identifier > 0)
                    {
                        locations[tp.Identifier] = tp;
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
            return output;
        }

        public List<TimingLocation> AddTimingLocations(List<TimingLocation> locs)
        {
            Log.D("MemStore", "AddTimingLocations");
            List<TimingLocation> output = database.AddTimingLocations(locs);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    foreach (TimingLocation tp in locs)
                    {
                        if (theEvent != null && tp.EventIdentifier == theEvent.Identifier && tp.Identifier > 0)
                        {
                            locations[tp.Identifier] = tp;
                        }
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
            return output;
        }

        public int GetTimingLocationId(TimingLocation tp)
        {
            Log.D("MemStore", "GetTimingLocationID");
            int output = -1;
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    foreach (TimingLocation loc in locations.Values.Where(loc => loc.Name.Equals(tp.Name, StringComparison.OrdinalIgnoreCase)
                                 && loc.EventIdentifier == tp.EventIdentifier))
                    {
                        output = loc.Identifier;
                        break;
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
            return output;
        }

        public List<TimingLocation> GetTimingLocations(int eventId)
        {
            Log.D("MemStore", "GetTimingLocations");
            List<TimingLocation> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        output.AddRange(locations.Values.Where(loc => loc.Identifier != Constants.Timing.LOCATION_FINISH && loc.Identifier != Constants.Timing.LOCATION_START && loc.Identifier != Constants.Timing.LOCATION_ANNOUNCER));
                    }
                    else
                    {
                        output.AddRange(database.GetTimingLocations(eventId));
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
            return output;
        }

        public void RemoveTimingLocation(TimingLocation tp)
        {
            Log.D("MemStore", "RemoveTimingLocation");
            database.RemoveTimingLocation(tp);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    locations.Remove(tp.Identifier);
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

        public void RemoveTimingLocation(int identifier)
        {
            Log.D("MemStore", "RemoveTimingLocation");
            database.RemoveTimingLocation(identifier);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    locations.Remove(identifier);
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

        public void UpdateTimingLocation(TimingLocation tp)
        {
            Log.D("MemStore", "RemoveTimingLocation");
            database.UpdateTimingLocation(tp);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (locations.TryGetValue(tp.Identifier, out TimingLocation? loc))
                    {
                        loc.CopyFrom(tp);
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
