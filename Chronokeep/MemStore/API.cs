using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;

namespace Chronokeep.MemStore
{
    internal partial class MemStore : IDBInterface
    {
        /**
         * API functions
         */

        public int AddAPI(ApiObject anAPI)
        {
            Log.D("MemStore", "UpdateAgeGroup");
            anAPI.Identifier = database.AddAPI(anAPI);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        apis[anAPI.Identifier] = anAPI;
                    }
                    finally
                    {
                        memStoreLock.Exit();
                    }
                }
                return anAPI.Identifier;
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring apiLock. " + e.Message);
                throw new ChronoLockException("apiLock");
            }
        }

        public List<ApiObject> GetAllAPI()
        {
            Log.D("MemStore", "GetAllAPI");
            List<ApiObject> output = [];
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        output.AddRange(apis.Values);
                    }
                    finally
                    {
                        memStoreLock.Exit();
                    }
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring apiLock. " + e.Message);
                throw new ChronoLockException("apiLock");
            }
            return output;
        }

        public ApiObject? GetAPI(int identifier)
        {
            Log.D("MemStore", "GetAPI");
            ApiObject? output = null;
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        if (!apis.TryGetValue(identifier, out output))
                        {
                            output = null;
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
                Log.D("MemStore", "Exception acquiring apiLock. " + e.Message);
                throw new ChronoLockException("apiLock");
            }
            return output;
        }

        public void RemoveAPI(int identifier)
        {
            Log.D("MemStore", "RemoveAPI");
            database.RemoveAPI(identifier);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        apis.Remove(identifier);
                    }
                    finally
                    {
                        memStoreLock.Exit();
                    }
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring apiLock. " + e.Message);
                throw new ChronoLockException("apiLock");
            }
        }

        public void UpdateAPI(ApiObject anAPI)
        {
            Log.D("MemStore", "UpdateAPI");
            database.UpdateAPI(anAPI);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        if (apis.TryGetValue(anAPI.Identifier, out ApiObject? api))
                        {
                            api.Type = anAPI.Type;
                            api.Url = anAPI.Url;
                            api.AuthToken = anAPI.AuthToken;
                            api.Nickname = anAPI.Nickname;
                            api.WebUrl = anAPI.WebUrl;
                        }
                        else
                        {
                            apis[anAPI.Identifier] = anAPI;
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
                Log.D("MemStore", "Exception acquiring apiLock. " + e.Message);
                throw new ChronoLockException("apiLock");
            }
        }
    }
}
