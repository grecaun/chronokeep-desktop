using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;

namespace Chronokeep.MemStore
{
    internal partial class MemStore : IDBInterface
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
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        if (theEvent != null && theEvent.Identifier == eventId)
                        {
                            Dictionary<int, DistanceStat> distStatDict = new();
                            DistanceStat allstats = new()
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
                                    distStats = new()
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
                                if (Constants.Timing.EVENTSPECIFIC_DNF == p.Status)
                                {
                                    distStats.Dnf += 1;
                                    allstats.Dnf += 1;
                                }
                                else if (Constants.Timing.EVENTSPECIFIC_FINISHED == p.Status)
                                {
                                    distStats.Finished += 1;
                                    allstats.Finished += 1;
                                }
                                else if (Constants.Timing.EVENTSPECIFIC_STARTED == p.Status)
                                {
                                    distStats.Active += 1;
                                    allstats.Active += 1;
                                }
                                else if (Constants.Timing.EVENTSPECIFIC_DNS == p.Status || Constants.Timing.EVENTSPECIFIC_UNKNOWN == p.Status)
                                {
                                    distStats.Dns += 1;
                                    allstats.Dns += 1;
                                }
                            }
                            output.AddRange(distStatDict.Values);
                            output.Sort((x1, x2) => x1.Active != x2.Active ? x2.Active.CompareTo(x1.Active) : x1.DistanceName.CompareTo(x2.DistanceName));
                            if (output.Count > 1)
                            {
                                output.Insert(0, allstats);
                            }
                        }
                    }
                    finally
                    {
                        memStoreLock.Exit();
                    }
                }
                return output;
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring distanceLock. " + e.Message);
                throw new ChronoLockException("distanceLock");
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
