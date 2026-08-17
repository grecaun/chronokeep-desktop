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
using Chronokeep.Objects.ChronokeepRemote;
using System;
using System.Collections.Generic;

namespace Chronokeep.MemStore
{
    internal partial class MemStore
    {
        /**
         * RemoteReader Functions
         */

        public void AddRemoteReaders(int eventId, List<RemoteReader> readers)
        {
            Log.D("MemStore", "AddRemoteReaders");
            database.AddRemoteReaders(eventId, readers);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent == null || theEvent.Identifier != eventId) return;
                    foreach (RemoteReader reader in readers)
                    {
                        remoteReaders.RemoveAll(x => reader.ApiiDentifier == x.ApiiDentifier && reader.Name.Equals(x.Name, StringComparison.OrdinalIgnoreCase));
                        remoteReaders.Add(reader);
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

        public void DeleteRemoteReader(int eventId, RemoteReader reader)
        {
            Log.D("MemStore", "DeleteRemoteReader");
            database.DeleteRemoteReader(eventId, reader);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        remoteReaders.RemoveAll(x => reader.ApiiDentifier == x.ApiiDentifier && reader.Name.Equals(x.Name, StringComparison.OrdinalIgnoreCase));
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

        public void DeleteRemoteReaders(int eventId, List<RemoteReader> readers)
        {
            Log.D("MemStore", "DeleteRemoteReaders");
            database.DeleteRemoteReaders(eventId, readers);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (theEvent == null || theEvent.Identifier != eventId) return;
                    foreach (RemoteReader reader in readers)
                    {
                        remoteReaders.RemoveAll(x => reader.ApiiDentifier == x.ApiiDentifier && reader.Name.Equals(x.Name, StringComparison.OrdinalIgnoreCase));
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

        public List<RemoteReader> GetRemoteReaders(int eventId)
        {
            Log.D("MemStore", "GetRemoteReaders");
            List<RemoteReader> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (theEvent != null && theEvent.Identifier == eventId)
                    {
                        output.AddRange(remoteReaders);
                    }
                    else
                    {
                        output.AddRange(database.GetRemoteReaders(eventId));
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
    }
}

