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
         * Event Functions
         */

        public int AddEvent(Event anEvent)
        {
            Log.D("MemStore", "AddEvent");
            int output = database.AddEvent(anEvent);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    anEvent.Identifier = output;
                    allEvents.Add(anEvent);
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

        public Event? GetCurrentEvent()
        {
            Log.D("MemStore", "GetCurrentEvent");
            Event? output = null;
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output = theEvent;
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

        public void SetCurrentEvent(int eventId)
        {
            Log.D("MemStore", "SetCurrentEvent");
            database.SetCurrentEvent(eventId);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    LoadEvent();
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

        public Event? GetEvent(int id)
        {
            Log.D("MemStore", "GetEvent");
            Event? output = null;
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    foreach (Event ev in allEvents.Where(ev => ev.Identifier == id))
                    {
                        output = ev;
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

        public int GetEventId(Event anEvent)
        {
            Log.D("MemStore", "GetEventID");
            int output = -1;
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    foreach (Event ev in allEvents.Where(ev => ev.Name.Equals(anEvent.Name, StringComparison.OrdinalIgnoreCase) && ev.Date.Equals(anEvent.Date, StringComparison.OrdinalIgnoreCase)))
                    {
                        output = ev.Identifier;
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

        public List<Event> GetEvents()
        {
            Log.D("MemStore", "GetEvents");
            List<Event> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output.AddRange(allEvents);
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

        public void RemoveEvent(int identifier)
        {
            Log.D("MemStore", "RemoveEvent");
            database.RemoveEvent(identifier);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    allEvents.RemoveAll(x => x.Identifier == identifier);
                    if (theEvent != null && theEvent.Identifier == identifier)
                    {
                        LoadEvent();
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

        public void RemoveEvent(Event anEvent)
        {
            Log.D("MemStore", "RemoveEvent");
            database.RemoveEvent(anEvent);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    allEvents.RemoveAll(x => x.Identifier == anEvent.Identifier);
                    if (theEvent != null && theEvent.Identifier == anEvent.Identifier)
                    {
                        LoadEvent();
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

        public void UpdateEvent(Event anEvent)
        {
            Log.D("MemStore", "UpdateEvent");
            database.UpdateEvent(anEvent);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent != null && theEvent.Identifier == anEvent.Identifier)
                    {
                        theEvent.CopyFrom(anEvent);
                    }
                    foreach (Event ev in allEvents.Where(ev => ev.Identifier == anEvent.Identifier))
                    {
                        ev.CopyFrom(anEvent);
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
        }
        public void UpdateDivisionsEnabled()
        {
            Log.D("MemStore", "UpdateDivisionsEnabled");
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent == null) return;
                    // Update all TimingResults in MemStore
                    foreach (TimeResult tr in timingResults.Values)
                    {
                        tr.UpdateEvent(theEvent);
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

        public void UpdateStart()
        {
            Log.D("MemStore", "UpdateStart");
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    // Update start in chip reads
                    DateTime start = DateTime.Now;
                    if (theEvent != null)
                    {
                        start = DateTime.Parse(theEvent.Date).AddSeconds(theEvent.StartSeconds).AddMilliseconds(theEvent.StartMilliseconds);
                    }
                    foreach (ChipRead cr in chipReads.Values)
                    {
                        cr.Start = start;
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

        public void SetFinishOptions(Event anEvent)
        {
            Log.D("MemStore", "SetFinishOptions");
            database.SetFinishOptions(anEvent);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent != null && theEvent.Identifier == anEvent.Identifier)
                    {
                        theEvent.FinishIgnoreWithin = anEvent.FinishIgnoreWithin;
                        theEvent.FinishMaxOccurrences = anEvent.FinishMaxOccurrences;
                    }
                    foreach (Event ev in allEvents.Where(ev => ev.Identifier == anEvent.Identifier))
                    {
                        ev.FinishIgnoreWithin = anEvent.FinishIgnoreWithin;
                        ev.FinishMaxOccurrences = anEvent.FinishMaxOccurrences;
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
        }

        public void SetStartOptions(Event anEvent)
        {
            Log.D("MemStore", "SetStartWindow");
            database.SetStartOptions(anEvent);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent != null && theEvent.Identifier == anEvent.Identifier)
                    {
                        theEvent.StartWindow = anEvent.StartWindow;
                    }
                    foreach (Event ev in allEvents.Where(ev => ev.Identifier == anEvent.Identifier))
                    {
                        ev.StartWindow = anEvent.StartWindow;
                        ev.StartMaxOccurrences = anEvent.StartMaxOccurrences;
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
        }

        public void SetStartFinishOptions(Event anEvent)
        {
            Log.D("MemStore", "SetStartFinishOptions");
            database.SetStartFinishOptions(anEvent);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent != null && theEvent.Identifier == anEvent.Identifier)
                    {
                        theEvent.StartWindow = anEvent.StartWindow;
                    }
                    foreach (Event ev in allEvents.Where(ev => ev.Identifier == anEvent.Identifier))
                    {
                        ev.StartWindow = anEvent.StartWindow;
                        ev.StartMaxOccurrences = anEvent.StartMaxOccurrences;
                        ev.FinishIgnoreWithin = anEvent.FinishIgnoreWithin;
                        ev.FinishMaxOccurrences = anEvent.FinishMaxOccurrences;
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
        }
    }
}