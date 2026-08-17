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
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
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
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
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
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
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
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
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
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }
    }
}
