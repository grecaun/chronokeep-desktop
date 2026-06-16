using Chronokeep.Helpers;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;

namespace Chronokeep.MemStore
{
    internal partial class MemStore
    {
        /**
         * Alarm Functions
         */

        public void DeleteAlarm(Alarm alarm)
        {
            Log.D("MemStore", "DeleteAlarms");
            database.DeleteAlarm(alarm);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    alarms.RemoveAll(x => alarm.Bib.Equals(x.Bib, StringComparison.OrdinalIgnoreCase) && alarm.Chip.Equals(x.Chip, StringComparison.OrdinalIgnoreCase));
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

        public void DeleteAlarms(int eventId)
        {
            Log.D("MemStore", "DeleteAlarms");
            database.DeleteAlarms(eventId);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        alarms.Clear();
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

        public List<Alarm> GetAlarms(int eventId)
        {
            Log.D("MemStore", "GetAlarms");
            List<Alarm> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        output.AddRange(alarms);
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
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public int SaveAlarm(int eventId, Alarm alarm)
        {
            Log.D("MemStore", "SaveAlarm");
            alarm.Identifier = database.SaveAlarm(eventId, alarm);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return alarm.Identifier;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        alarms.Add(alarm);
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
                return alarm.Identifier;
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public List<Alarm> SaveAlarms(int eventId, List<Alarm> iAlarms)
        {
            Log.D("MemStore", "SaveAlarms");
            List<Alarm> output = [];
            output.AddRange(database.SaveAlarms(eventId, iAlarms));
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        alarms.AddRange(output);
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
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }
    }
}