using Chronokeep.Helpers;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;

namespace Chronokeep.MemStore
{
    internal partial class MemStore
    {
        /**
         * API functions
         */

        public int AddApi(ApiObject anApi)
        {
            Log.D("MemStore", "UpdateAgeGroup");
            anApi.Identifier = database.AddApi(anApi);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return anApi.Identifier;
                try
                {
                    apis[anApi.Identifier] = anApi;
                }
                finally
                {
                    memStoreLock.Exit();
                }
                return anApi.Identifier;
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring apiLock. " + e.Message);
                throw new ChronokeepLockException("apiLock");
            }
        }

        public List<ApiObject> GetAllApi()
        {
            Log.D("MemStore", "GetAllAPI");
            List<ApiObject> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output.AddRange(apis.Values);
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring apiLock. " + e.Message);
                throw new ChronokeepLockException("apiLock");
            }
            return output;
        }

        public ApiObject? GetApi(int identifier)
        {
            Log.D("MemStore", "GetAPI");
            ApiObject? output = null;
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output = apis.GetValueOrDefault(identifier);
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring apiLock. " + e.Message);
                throw new ChronokeepLockException("apiLock");
            }
            return output;
        }

        public void RemoveApi(int identifier)
        {
            Log.D("MemStore", "RemoveAPI");
            database.RemoveApi(identifier);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    apis.Remove(identifier);
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring apiLock. " + e.Message);
                throw new ChronokeepLockException("apiLock");
            }
        }

        public void UpdateApi(ApiObject anApi)
        {
            Log.D("MemStore", "UpdateAPI");
            database.UpdateApi(anApi);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (apis.TryGetValue(anApi.Identifier, out ApiObject? api))
                    {
                        api.Type = anApi.Type;
                        api.Url = anApi.Url;
                        api.AuthToken = anApi.AuthToken;
                        api.Nickname = anApi.Nickname;
                        api.WebUrl = anApi.WebUrl;
                    }
                    else
                    {
                        apis[anApi.Identifier] = anApi;
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring apiLock. " + e.Message);
                throw new ChronokeepLockException("apiLock");
            }
        }
    }
}