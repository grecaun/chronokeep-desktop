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
         * Age Group Functions
         */

        private static void SetAgeGroups()
        {
            currentAgeGroups.Clear();
            lastAgeGroup.Clear();
            foreach (AgeGroup g in ageGroups.Values.SelectMany(groups => groups))
            {
                for (int i = g.StartAge; i <= g.EndAge; i++)
                {
                    currentAgeGroups[(g.DistanceId, i)] = g;
                }

                if (lastAgeGroup.TryGetValue(g.DistanceId, out AgeGroup? group) &&
                    group.StartAge >= g.StartAge) continue;
                group = g;
                lastAgeGroup[g.DistanceId] = group;
            }
        }

        public int AddAgeGroup(AgeGroup group)
        {
            Log.D("MemStore", "AddAgeGroup");
            group.GroupId = database.AddAgeGroup(group);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return group.GroupId;
                try
                {
                    if (!ageGroups.TryGetValue(group.DistanceId, out List<AgeGroup>? value))
                    {
                        value = [];
                        ageGroups[group.DistanceId] = value;
                    }
                    value.Add(group);
                    SetAgeGroups();
                }
                finally
                {
                    memStoreLock.Exit();
                }
                return group.GroupId;
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public List<AgeGroup> AddAgeGroups(List<AgeGroup> groups)
        {
            Log.D("MemStore", "AddAgeGroups");
            List<AgeGroup> output = database.AddAgeGroups(groups);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    foreach (AgeGroup group in output)
                    {
                        if (!ageGroups.TryGetValue(group.DistanceId, out List<AgeGroup>? value))
                        {
                            value = [];
                            ageGroups[group.DistanceId] = value;
                        }
                        value.Add(group);
                    }
                    SetAgeGroups();
                }
                finally
                {
                    memStoreLock.Exit();
                }
                return output;
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public List<AgeGroup> GetAgeGroups(int eventId)
        {
            Log.D("MemStore", "GetAgeGroups");
            List<AgeGroup> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        foreach (List<AgeGroup> groups in ageGroups.Values)
                        {
                            output.AddRange(groups);
                        }
                    }
                    else
                    {
                        output.AddRange(database.GetAgeGroups(eventId));
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
                return output;
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public List<AgeGroup> GetAgeGroups(int eventId, int distanceId)
        {
            Log.D("MemStore", "GetAgeGroups");
            List<AgeGroup> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        if (ageGroups.TryGetValue(distanceId, out List<AgeGroup>? groups))
                        {
                            output.AddRange(groups);
                        }
                    }
                    else
                    {
                        output.AddRange(database.GetAgeGroups(eventId, distanceId));
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
                return output;
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public void RemoveAgeGroup(AgeGroup group)
        {
            Log.D("MemStore", "RemoveAgeGroup");
            database.RemoveAgeGroup(group);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (ageGroups.TryGetValue(group.DistanceId, out List<AgeGroup>? list))
                    {
                        list.Remove(group);
                    }
                    SetAgeGroups();
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

        public void RemoveAgeGroups(int eventId, int distanceId)
        {
            Log.D("MemStore", "RemoveAgeGroup");
            database.RemoveAgeGroups(eventId, distanceId);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent == null || theEvent.Identifier != eventId) return;
                    if (ageGroups.TryGetValue(distanceId, out List<AgeGroup>? list))
                    {
                        list.Clear();
                    }
                    SetAgeGroups();
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

        public void RemoveAgeGroups(List<AgeGroup> groups)
        {
            Log.D("MemStore", "RemoveAgeGroups");
            database.RemoveAgeGroups(groups);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    foreach (AgeGroup group in groups)
                    {
                        if (ageGroups.TryGetValue(group.DistanceId, out List<AgeGroup>? list))
                        {
                            list.Remove(group);
                        }
                    }
                    SetAgeGroups();
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

        public void ResetAgeGroups(int eventId)
        {
            Log.D("MemStore", "ResetAgeGroups");
            database.ResetAgeGroups(eventId);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent == null || theEvent.Identifier != eventId) return;
                    ageGroups.Clear();
                    SetAgeGroups();
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

        public void UpdateAgeGroup(AgeGroup group)
        {
            Log.D("MemStore", "UpdateAgeGroup");
            database.UpdateAgeGroup(group);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (ageGroups.TryGetValue(group.DistanceId, out List<AgeGroup>? list))
                    {
                        foreach (AgeGroup ageGroup in list.Where(ageGroup => ageGroup.GroupId == group.GroupId))
                        {
                            ageGroup.StartAge = group.StartAge;
                            ageGroup.EndAge = group.EndAge;
                            ageGroup.LastGroup = group.LastGroup;
                            ageGroup.CustomName = group.CustomName;
                        }
                    }
                    SetAgeGroups();
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
