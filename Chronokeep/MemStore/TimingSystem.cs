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
         * TimingSystem Functions
         */

        public int AddTimingSystem(TimingSystem system)
        {
            Log.D("MemStore", "AddTimingSystem");
            int output = database.AddTimingSystem(system);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    system.SystemIdentifier = output;
                    timingSystems[system.IpAddress.Trim()] = system;
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

        public List<TimingSystem> GetTimingSystems()
        {
            Log.D("MemStore", "GetTimingSystems");
            List<TimingSystem> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output.AddRange(timingSystems.Values);
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

        public void RemoveTimingSystem(TimingSystem system)
        {
            Log.D("MemStore", "RemoveTimingSystem");
            database.RemoveTimingSystem(system);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    timingSystems.Remove(system.IpAddress.Trim());
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

        public void RemoveTimingSystem(int systemId)
        {
            Log.D("MemStore", "RemoveTimingSystem");
            database.RemoveTimingSystem(systemId);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    string ip = "";
                    foreach (TimingSystem system in timingSystems.Values.Where(system => system.SystemIdentifier == systemId))
                    {
                        ip = system.IpAddress.Trim();
                        break;
                    }
                    if (ip.Length > 0)
                    {
                        timingSystems.Remove(ip);
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

        public void SetTimingSystems(List<TimingSystem> systems)
        {
            Log.D("MemStore", "SetTimingSystems");
            database.SetTimingSystems(systems);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    timingSystems.Clear();
                    foreach (TimingSystem system in systems)
                    {
                        timingSystems[system.IpAddress.Trim()] = system;
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

        public void UpdateTimingSystem(TimingSystem system)
        {
            Log.D("MemStore", "UpdateTimingSystem");
            database.UpdateTimingSystem(system);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (timingSystems.TryGetValue(system.IpAddress.Trim(), out TimingSystem? oldSystem))
                    {
                        oldSystem.CopyFrom(system);
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
