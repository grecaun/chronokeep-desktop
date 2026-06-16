using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Objects.ChronoKeepAPI;
using System;
using System.Collections.Generic;

namespace Chronokeep.MemStore
{
    internal partial class MemStore : IDBInterface
    {
        /**
         * EmailAlert Functions
         */

        public void AddEmailAlert(int eventId, int eventspecific_id)
        {
            Log.D("MemStore", "AddEmailAlert");
            database.AddEmailAlert(eventId, eventspecific_id);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        if (theEvent != null && theEvent.Identifier == eventId)
                        {
                            emailAlerts.Add(eventspecific_id);
                        }
                    }
                    finally
                    {
                        memStoreLock.Exit();
                    }
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        public List<int> GetEmailAlerts(int eventId)
        {
            Log.D("MemStore", "GetEmailAlerts");
            List<int> output = [];
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
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
                }
                return output;
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        /**
         * SMS Functions
         */

        public void AddSMSAlert(int eventId, int eventspecific_id, int segment_id)
        {
            Log.D("MemStore", "AddSMSAlert");
            database.AddSMSAlert(eventId, eventspecific_id, segment_id);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        if (theEvent != null && theEvent.Identifier == eventId)
                        {
                            smsAlerts.Add((eventspecific_id, segment_id));
                        }
                    }
                    finally
                    {
                        memStoreLock.Exit();
                    }
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        public void AddSmsSubscriptions(int eventId, List<ApiSmsSubscription> subscriptions)
        {
            Log.D("MemStore", "AddSmsSubscriptions");
            database.AddSmsSubscriptions(eventId, subscriptions);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
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
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        public void DeleteSmsSubscriptions(int eventId)
        {
            Log.D("MemStore", "DeleteSmsSubscriptions");
            database.DeleteSmsSubscriptions(eventId);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
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
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        public List<(int, int)> GetSMSAlerts(int eventId)
        {
            Log.D("MemStore", "GetSMSAlerts");
            List<(int, int)> output = [];
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
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
                }
                return output;
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        public List<ApiSmsSubscription> GetSmsSubscriptions(int eventId)
        {
            Log.D("MemStore", "GetSmsSubscriptions");
            List<ApiSmsSubscription> output = [];
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
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
                }
                return output;
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }
    }
}
