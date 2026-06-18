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
         * TimingResult Functions
         */

        public void AddTimingResult(TimeResult tr)
        {
            Log.D("MemStore", "AddTimingResult");
            database.AddTimingResult(tr);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    string bib = "";
                    if (participants.TryGetValue(tr.EventSpecificId, out Participant? p))
                    {
                        bib = p.Bib;
                        tr.SetParticipant(p);
                        if (distances.TryGetValue(p.EventSpecific.DistanceIdentifier, out Distance? distance))
                        {
                            tr.SetResultType(distance.Type);
                            if (distance.LinkedDistance != Constants.Timing.DISTANCE_NO_LINKED_ID && distances.TryGetValue(distance.LinkedDistance, out Distance? linked))
                            {
                                tr.SetLinkedDistanceName(linked.Name);
                            }
                        }
                    }
                    else
                    {
                        tr.SetBlankParticipant();
                    }
                    if (chipReads.TryGetValue(tr.ReadId, out ChipRead? chipRead))
                    {
                        if (bib.Length < 1)
                        {
                            bib = chipRead.Bib;
                        }
                        tr.SetChipRead(chipRead.ChipNumber, bib, chipRead.TimeSeconds, chipRead.TimeMilliseconds);
                    }
                    else
                    {
                        tr.SetChipRead("", bib, 0, 0);
                    }
                    tr.SetFinalValues(
                        locations,
                        segments,
                        distanceNameDict,
                        theEvent!
                    );
                    timingResults[(tr.EventSpecificId, tr.LocationId, tr.Occurrence, tr.UnknownId)] = tr;
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public void AddTimingResults(List<TimeResult> results)
        {
            Log.D("MemStore", "AddTimingResults");
            database.AddTimingResults(results);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    foreach (TimeResult tr in results)
                    {
                        string bib = "";
                        if (participants.TryGetValue(tr.EventSpecificId, out Participant? p))
                        {
                            bib = p.Bib;
                            tr.SetParticipant(p);
                            if (distances.TryGetValue(p.EventSpecific.DistanceIdentifier, out Distance? distance))
                            {
                                tr.SetResultType(distance.Type);
                                if (distance.LinkedDistance != Constants.Timing.DISTANCE_NO_LINKED_ID && distances.TryGetValue(distance.LinkedDistance, out Distance? linked))
                                {
                                    tr.SetLinkedDistanceName(linked.Name);
                                }
                            }
                        }
                        else
                        {
                            tr.SetBlankParticipant();
                        }
                        if (chipReads.TryGetValue(tr.ReadId, out ChipRead? chipRead))
                        {
                            if (bib.Length < 1)
                            {
                                bib = chipRead.Bib;
                            }
                            tr.SetChipRead(chipRead.ChipNumber, bib, chipRead.TimeSeconds, chipRead.TimeMilliseconds);
                        }
                        else
                        {
                            tr.SetChipRead("", bib, 0, 0);
                        }
                        tr.SetFinalValues(
                            locations,
                            segments,
                            distanceNameDict,
                            theEvent!
                        );
                        timingResults[(tr.EventSpecificId, tr.LocationId, tr.Occurrence, tr.UnknownId)] = tr;
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public List<TimeResult> GetSegmentTimes(int eventId, int segmentId)
        {
            Log.D("MemStore", "GetSegmentTimes");
            List<TimeResult> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        output.AddRange(timingResults.Values.Where(tr => tr.EventIdentifier == eventId && tr.SegmentId == segmentId));
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
            return output;
        }

        public List<TimeResult> GetFinishTimes(int eventId)
        {
            Log.D("MemStore", "GetFinishTimes");
            return GetSegmentTimes(eventId, Constants.Timing.SEGMENT_FINISH);
        }

        public List<TimeResult> GetLastSeenResults(int eventId)
        {
            Log.D("MemStore", "GetLastSeenResults");
            List<TimeResult> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        Dictionary<int, TimeResult> trDict = new();
                        foreach (TimeResult tr in timingResults.Values)
                        {
                            if (trDict.TryGetValue(tr.EventSpecificId, out TimeResult? result))
                            {
                                if (result.Seconds < tr.Seconds || (result.Seconds == tr.Seconds && result.Milliseconds < tr.Milliseconds))
                                {
                                    trDict[tr.EventSpecificId] = tr;
                                }
                            }
                            else
                            {
                                trDict[tr.EventSpecificId] = tr;
                            }
                        }
                        output.AddRange(trDict.Values);
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
            return output;
        }

        public List<TimeResult> GetNonUploadedResults(int eventId)
        {
            Log.D("MemStore", "GetNonUploadedResults");
            List<TimeResult> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        output.AddRange(timingResults.Values.Where(tr => tr.EventIdentifier == eventId && !tr.IsUploaded() && tr.DistanceName.Length > 0));
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
            return output;
        }

        public List<TimeResult> GetStartTimes(int eventId)
        {
            Log.D("MemStore", "GetStartTimes");
            return GetSegmentTimes(eventId, Constants.Timing.SEGMENT_START);
        }

        public List<TimeResult> GetTimingResults(int eventId)
        {
            Log.D("MemStore", "GetTimingResults");
            List<TimeResult> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        output.AddRange(timingResults.Values);
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
            return output;
        }

        public void RemoveTimingResult(TimeResult tr)
        {
            Log.D("MemStore", "RemoveTimingResult");
            database.RemoveTimingResult(tr);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    timingResults.Remove((tr.EventSpecificId, tr.LocationId, tr.Occurrence, tr.UnknownId));
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public void SetUploadedTimingResults(List<TimeResult> results)
        {
            Log.D("MemStore", "SetUploadedTimingResults");
            database.SetUploadedTimingResults(results);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    foreach (TimeResult changed in results)
                    {
                        foreach (TimeResult toChange in timingResults.Values.Where(toChange => changed.Equals(toChange)))
                        {
                            toChange.Uploaded = changed.Uploaded;
                            break;
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
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public bool UnprocessedResultsExist(int eventId)
        {
            Log.D("MemStore", "UnprocessedResultsExist");
            bool output = false;
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        if (timingResults.Values.Any(result => result.Status == Constants.Timing.TIMERESULT_STATUS_NONE))
                        {
                            output = true;
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
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
            return output;
        }

        // Reset Timing Results clears all timing results
        // But it also changes ChipReads statuses to None except where
        // statuses were set to IGNORE/DNF/DNS/DNF_IGNORE/DNS_IGNORE,
        // and it also sets Participant EVENTSPECIFIC_STATUS to UNKNOWN
        public void ResetTimingResultsEvent(int eventId)
        {
            Log.D("MemStore", "ResetTimingResultsEvent");
            database.ResetTimingResultsEvent(eventId);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent == null || theEvent.Identifier != eventId) return;
                    timingResults.Clear();
                    foreach (ChipRead cr in chipReads.Values.Where(cr => cr.CanBeReset()))
                    {
                        cr.Status = Constants.Timing.CHIPREAD_STATUS_NONE;
                    }
                    foreach (Participant p in participants.Values)
                    {
                        p.EventSpecific.Status = Constants.Timing.EVENTSPECIFIC_UNKNOWN;
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public void ResetTimingResultsPlacements(int eventId)
        {
            Log.D("MemStore", "ResetTimingResultsPlacements");
            database.ResetTimingResultsPlacements(eventId);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent == null || theEvent.Identifier != eventId) return;
                    foreach (TimeResult tr in timingResults.Values)
                    {
                        tr.Status = Constants.Timing.CHIPREAD_STATUS_NONE;
                        tr.Place = -1;
                        tr.AgePlace = -1;
                        tr.GenderPlace = -1;
                        tr.Uploaded = Constants.Timing.TIMERESULT_UPLOADED_FALSE;
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }
    }
}
