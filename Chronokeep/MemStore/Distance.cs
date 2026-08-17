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
using System.Linq;

namespace Chronokeep.MemStore
{
    internal partial class MemStore
    {
        /**
         * Distance Functions
         */

        public int AddDistance(Distance dist)
        {
            Log.D("MemStore", "AddDistance");
            dist.Identifier = database.AddDistance(dist);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return dist.Identifier;
                try
                {
                    if (theEvent != null && dist.EventIdentifier == theEvent.Identifier && dist.Identifier > 0)
                    {
                        distances[dist.Identifier] = dist;
                        distanceNameDict[dist.Name] = dist;
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
            return dist.Identifier;
        }

        public List<Distance> AddDistances(List<Distance> dists)
        {
            Log.D("MemStore", "AddDistances");
            List<Distance> output = database.AddDistances(dists);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    foreach (Distance dist in output)
                    {
                        if (theEvent == null || dist.EventIdentifier != theEvent.Identifier || dist.Identifier <= 0)
                            continue;
                        dists[dist.Identifier] = dist;
                        distanceNameDict[dist.Name] = dist;
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

        public Distance? GetDistance(int divId)
        {
            Log.D("MemStore", "GetDistance");
            Distance? output = null;
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    distances.TryGetValue(divId, out output);
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

        public int GetDistanceId(Distance dist)
        {
            Log.D("MemStore", "GetDistanceID");
            int output = -1;
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    foreach (Distance known in distances.Values.Where(known => known.Name.Equals(dist.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        output = known.Identifier;
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

        public List<Distance> GetDistances(int eventId)
        {
            Log.D("MemStore", "GetDistances");
            List<Distance> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        output.AddRange(distances.Values);
                    }
                    else
                    {
                        output.AddRange(database.GetDistances(eventId));
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

        public void RemoveDistance(int identifier)
        {
            Log.D("MemStore", "RemoveDistance");
            database.RemoveDistance(identifier);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    distances.Remove(identifier);
                    string distName = "";
                    foreach (Distance dist in distances.Values.Where(dist => dist.Identifier == identifier))
                    {
                        distName = dist.Name;
                        break;
                    }
                    if (distName.Length > 0)
                    {
                        distanceNameDict.Remove(distName);
                    }
                    List<int> participantsToRemove = (from p in participants.Values where p.EventSpecific.DistanceIdentifier == identifier select p.EventSpecific.Identifier).ToList();
                    foreach (int i in participantsToRemove)
                    {
                        participants.Remove(i);
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

        public void RemoveDistance(Distance dist)
        {
            Log.D("MemStore", "RemoveDistance");
            database.RemoveDistance(dist);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    distances.Remove(dist.Identifier);
                    distanceNameDict.Remove(dist.Name);
                    List<int> participantsToRemove = (from p in participants.Values where p.EventSpecific.DistanceIdentifier == dist.Identifier select p.EventSpecific.Identifier).ToList();
                    foreach (int i in participantsToRemove)
                    {
                        participants.Remove(i);
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

        public void UpdateDistance(Distance dist)
        {
            Log.D("MemStore", "UpdateDistance");
            database.UpdateDistance(dist);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    Dictionary<string, string> oldDistanceNameDict = new();
                    foreach (Distance old in distances.Values.Where(dist.Equals))
                    {
                        if (!dist.Name.Equals(old.Name))
                        {
                            oldDistanceNameDict[old.Name] = dist.Name;
                        }
                        old.Update(dist);
                    }
                    foreach (Distance old in distanceNameDict.Values.Where(dist.Equals))
                    {
                        old.Update(dist);
                    }
                    foreach (Participant p in participants.Values.Where(p => p.EventSpecific.DistanceIdentifier == dist.Identifier))
                    {
                        p.EventSpecific.DistanceName = dist.Name;
                    }
                    foreach (TimeResult res in timingResults.Values)
                    {
                        if (oldDistanceNameDict.TryGetValue(res.RealDistanceName, out string? newDistName))
                        {
                            res.RealDistanceName = newDistName;
                        }
                        if (res.LinkedDistanceName.Length > 0 && oldDistanceNameDict.TryGetValue(res.LinkedDistanceName, out string? newDistanceName))
                        {
                            res.LinkedDistanceName = newDistanceName;
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
        }

        public void SetWaveTimes(int eventId, int wave, long seconds, int milliseconds)
        {
            Log.D("MemStore", "SetWaveTimes");
            database.SetWaveTimes(eventId, wave, seconds, milliseconds);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent == null || theEvent.Identifier != eventId) return;
                    foreach (Distance old in distances.Values.Where(old => old.Wave == wave))
                    {
                        old.SetWaveTime(wave, seconds, milliseconds);
                    }
                    foreach (Distance old in distanceNameDict.Values.Where(old => old.Wave == wave))
                    {
                        old.SetWaveTime(wave, seconds, milliseconds);
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
