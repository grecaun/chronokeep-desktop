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
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
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
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
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
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
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
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
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
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
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
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }
    }
}

