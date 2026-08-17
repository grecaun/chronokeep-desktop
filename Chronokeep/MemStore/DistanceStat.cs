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
         * DistanceStat Functions
         */

        public List<DistanceStat> GetDistanceStats(int eventId, bool condense = false)
        {
            Log.D("MemStore", "GetTimingSystems");
            List<DistanceStat> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        Dictionary<int, DistanceStat> distStatDict = new();
                        DistanceStat allStats = new()
                        {
                            DistanceName = "All",
                            DistanceId = -1,
                            Active = 0,
                            Dnf = 0,
                            Dns = 0,
                            Finished = 0
                        };
                        foreach (Participant p in participants.Values)
                        {
                            int distIdent = p.EventSpecific.DistanceIdentifier;
                            string distName = distances.TryGetValue(distIdent, out Distance? dist) ? dist.Name : "";
                            if (condense && dist != null && dist.LinkedDistance != Constants.Timing.DISTANCE_DUMMYIDENTIFIER && distances.TryGetValue(dist.LinkedDistance, out Distance? linkDist))
                            {
                                distName = linkDist.Name;
                                distIdent = linkDist.Identifier;
                            }
                            if (!distStatDict.TryGetValue(distIdent, out DistanceStat? distStats))
                            {
                                distStats = new DistanceStat
                                {
                                    DistanceName = distName,
                                    DistanceId = distIdent,
                                    Active = 0,
                                    Dnf = 0,
                                    Dns = 0,
                                    Finished = 0
                                };
                                distStatDict[distIdent] = distStats;
                            }
                            switch (p.Status)
                            {
                                case Constants.Timing.EVENTSPECIFIC_DNF:
                                    distStats.Dnf += 1;
                                    allStats.Dnf += 1;
                                    break;
                                case Constants.Timing.EVENTSPECIFIC_FINISHED:
                                    distStats.Finished += 1;
                                    allStats.Finished += 1;
                                    break;
                                case Constants.Timing.EVENTSPECIFIC_STARTED:
                                    distStats.Active += 1;
                                    allStats.Active += 1;
                                    break;
                                case Constants.Timing.EVENTSPECIFIC_DNS:
                                case Constants.Timing.EVENTSPECIFIC_UNKNOWN:
                                    distStats.Dns += 1;
                                    allStats.Dns += 1;
                                    break;
                            }
                        }
                        output.AddRange(distStatDict.Values);
                        output.Sort((x1, x2) => x1.Active != x2.Active ? x2.Active.CompareTo(x1.Active) : string.Compare(x1.DistanceName, x2.DistanceName, StringComparison.Ordinal));
                        if (output.Count > 1)
                        {
                            output.Insert(0, allStats);
                        }
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
                Log.D("MemStore", $"Exception acquiring distanceLock. {e.Message}");
                throw new ChronokeepLockException("distanceLock");
            }
        }

        public Dictionary<int, List<Participant>> GetDistanceParticipantsStatus(int eventId, int distanceId)
        {
            Dictionary<int, List<Participant>> output = [];
            List<Participant> dbParts = (distanceId == -1) ? GetParticipants(eventId) : GetParticipants(eventId, distanceId);
            foreach (Participant person in dbParts)
            {
                if (!output.TryGetValue(person.Status, out List<Participant>? localParts))
                {
                    localParts = [];
                    output[person.Status] = localParts;
                }
                localParts.Add(person);
            }
            return output;
        }
    }
}

