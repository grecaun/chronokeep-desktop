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
using Chronokeep.Objects.ChronoKeepAPI;
using System;
using System.Collections.Generic;

namespace Chronokeep.MemStore
{
    internal partial class MemStore
    {
        /**
         * EmailAlert Functions
         */

        public void AddEmailAlert(int eventId, int eventspecificId)
        {
            Log.D("MemStore", "AddEmailAlert");
            database.AddEmailAlert(eventId, eventspecificId);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        emailAlerts.Add(eventspecificId);
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

        public List<int> GetEmailAlerts(int eventId)
        {
            Log.D("MemStore", "GetEmailAlerts");
            List<int> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        output.AddRange(emailAlerts);
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

        /**
         * SMS Functions
         */

        public void AddSmsAlert(int eventId, int eventspecificId, int segmentId)
        {
            Log.D("MemStore", "AddSMSAlert");
            database.AddSmsAlert(eventId, eventspecificId, segmentId);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        smsAlerts.Add((eventspecificId, segmentId));
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

        public void AddSmsSubscriptions(int eventId, List<ApiSmsSubscription> subscriptions)
        {
            Log.D("MemStore", "AddSmsSubscriptions");
            database.AddSmsSubscriptions(eventId, subscriptions);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        smsSubscriptions.AddRange(subscriptions);
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

        public void DeleteSmsSubscriptions(int eventId)
        {
            Log.D("MemStore", "DeleteSmsSubscriptions");
            database.DeleteSmsSubscriptions(eventId);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        smsSubscriptions.Clear();
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

        public List<(int, int)> GetSmsAlerts(int eventId)
        {
            Log.D("MemStore", "GetSMSAlerts");
            List<(int, int)> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        output.AddRange(smsAlerts);
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

        public List<ApiSmsSubscription> GetSmsSubscriptions(int eventId)
        {
            Log.D("MemStore", "GetSmsSubscriptions");
            List<ApiSmsSubscription> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        output.AddRange(smsSubscriptions);
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
