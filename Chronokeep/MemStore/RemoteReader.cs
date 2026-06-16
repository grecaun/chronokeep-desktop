using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Objects.ChronokeepRemote;
using System;
using System.Collections.Generic;

namespace Chronokeep.MemStore
{
    internal partial class MemStore : IDBInterface
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
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        if (theEvent != null && theEvent.Identifier == eventId)
                        {
                            foreach (RemoteReader reader in readers)
                            {
                                remoteReaders.RemoveAll(x => reader.ApiiDentifier == x.ApiiDentifier && reader.Name.Equals(x.Name, StringComparison.OrdinalIgnoreCase));
                                remoteReaders.Add(reader);
                            }
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

        public void DeleteRemoteReader(int eventId, RemoteReader reader)
        {
            Log.D("MemStore", "DeleteRemoteReader");
            database.DeleteRemoteReader(eventId, reader);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
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
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        public void DeleteRemoteReaders(int eventId, List<RemoteReader> readers)
        {
            Log.D("MemStore", "DeleteRemoteReaders");
            database.DeleteRemoteReaders(eventId, readers);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        if (theEvent != null && theEvent.Identifier == eventId)
                        {
                            foreach (RemoteReader reader in readers)
                            {
                                remoteReaders.RemoveAll(x => reader.ApiiDentifier == x.ApiiDentifier && reader.Name.Equals(x.Name, StringComparison.OrdinalIgnoreCase));
                            }
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

        public List<RemoteReader> GetRemoteReaders(int eventId)
        {
            Log.D("MemStore", "GetRemoteReaders");
            List<RemoteReader> output = [];
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
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
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
            return output;
        }
    }
}
