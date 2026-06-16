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
         * Segment Functions
         */

        public int AddSegment(Segment seg)
        {
            Log.D("MemStore", "AddSegment");
            int output = database.AddSegment(seg);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && seg.EventId == theEvent.Identifier && seg.Identifier > 0)
                    {
                        seg.Identifier = output;
                    }
                    segments[seg.Identifier] = seg;
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring segmentLock. " + e.Message);
                throw new ChronokeepLockException("segmentLock");
            }
            return output;
        }

        public List<Segment> AddSegments(List<Segment> segs)
        {
            Log.D("MemStore", "AddSegments");
            List<Segment> output = database.AddSegments(segs);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    foreach (Segment seg in output)
                    {
                        if (theEvent != null && seg.EventId == theEvent.Identifier && seg.Identifier > 0)
                        {
                            segments[seg.Identifier] = seg;
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
                Log.D("MemStore", "Exception acquiring segmentLock. " + e.Message);
                throw new ChronokeepLockException("segmentLock");
            }
            return output;
        }

        public int GetSegmentId(Segment seg)
        {
            Log.D("MemStore", "GetSegmentId");
            int output = -1;
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    foreach (Segment s in segments.Values.Where(s => s.EventId == seg.EventId
                                                                     && s.DistanceId == seg.DistanceId
                                                                     && s.LocationId == seg.LocationId
                                                                     && s.Occurrence == seg.Occurrence))
                    {
                        output = s.Identifier;
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
                Log.D("MemStore", "Exception acquiring segmentLock. " + e.Message);
                throw new ChronokeepLockException("segmentLock");
            }
            return output;
        }

        public List<Segment> GetSegments(int eventId)
        {
            Log.D("MemStore", "GetSegments");
            List<Segment> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        output.AddRange(segments.Values);
                    }
                    else
                    {
                        output.AddRange(database.GetSegments(eventId));
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring segmentLock. " + e.Message);
                throw new ChronokeepLockException("segmentLock");
            }
            return output;
        }

        public int GetMaxSegments(int eventId)
        {
            Log.D("MemStore", "GetMaxSegments");
            int output = 0;
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        Dictionary<int, int> maxSegmentsPerDistance = new();
                        foreach (Segment s in segments.Values)
                        {
                            int count = maxSegmentsPerDistance.GetValueOrDefault(s.DistanceId, 0);
                            count++;
                            maxSegmentsPerDistance[s.DistanceId] = count;
                            if (count > output)
                            {
                                output = count;
                            }
                        }
                    }
                    else
                    {
                        output = database.GetMaxSegments(eventId);
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring segmentLock. " + e.Message);
                throw new ChronokeepLockException("segmentLock");
            }
            return output;
        }

        public void RemoveSegment(Segment seg)
        {
            Log.D("MemStore", "RemoveSegment");
            database.RemoveSegment(seg);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    segments.Remove(seg.Identifier);
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring segmentLock. " + e.Message);
                throw new ChronokeepLockException("segmentLock");
            }
        }

        public void RemoveSegment(int identifier)
        {
            Log.D("MemStore", "RemoveSegment");
            database.RemoveSegment(identifier);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    segments.Remove(identifier);
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring segmentLock. " + e.Message);
                throw new ChronokeepLockException("segmentLock");
            }
        }

        public void RemoveSegments(List<Segment> segs)
        {
            Log.D("MemStore", "RemoveSegments");
            database.RemoveSegments(segs);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    foreach (Segment s in segs)
                    {
                        segments.Remove(s.Identifier);
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring segmentLock. " + e.Message);
                throw new ChronokeepLockException("segmentLock");
            }
        }

        public void ResetSegments(int eventId)
        {
            Log.D("MemStore", "RemoveSegments");
            database.ResetSegments(eventId);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        segments.Clear();
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring segmentLock. " + e.Message);
                throw new ChronokeepLockException("segmentLock");
            }
        }

        public void UpdateSegment(Segment seg)
        {
            Log.D("MemStore", "UpdateSegment");
            database.UpdateSegment(seg);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (segments.TryGetValue(seg.Identifier, out Segment? oldSeg))
                    {
                        oldSeg.CopyFrom(seg);
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring segmentLock. " + e.Message);
                throw new ChronokeepLockException("segmentLock");
            }
        }

        public void UpdateSegments(List<Segment> segs)
        {
            Log.D("MemStore", "UpdateSegments");
            database.UpdateSegments(segs);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    foreach (Segment s in segs)
                    {
                        if (segments.TryGetValue(s.Identifier, out Segment? oldSeg))
                        {
                            oldSeg.CopyFrom(s);
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
                Log.D("MemStore", "Exception acquiring segmentLock. " + e.Message);
                throw new ChronokeepLockException("segmentLock");
            }
        }
    }
}
