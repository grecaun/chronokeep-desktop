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
         * API functions
         */

        public int AddApi(ApiObject anApi)
        {
            Log.D("MemStore", "UpdateAgeGroup");
            anApi.Identifier = database.AddApi(anApi);
            try
            {
                memStoreLock.Enter();
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
                Log.D("MemStore", $"Exception acquiring apiLock. {e.Message}");
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
                Log.D("MemStore", $"Exception acquiring apiLock. {e.Message}");
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
                Log.D("MemStore", $"Exception acquiring apiLock. {e.Message}");
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
                memStoreLock.Enter();
                try
                {
                    apis.Remove(identifier);
                    // Mirror the database: events linked to this API lose that link.
                    IEnumerable<Event> cached = theEvent == null ? allEvents : allEvents.Append(theEvent);
                    foreach (Event ev in cached)
                    {
                        if (ev.ApiId == identifier)
                        {
                            ev.ApiId = Constants.ApiConstants.NULL_ID;
                            ev.ApiEventId = Constants.ApiConstants.NULL_EVENT_ID;
                        }
                        if (ev.RegistrationApiId == identifier)
                        {
                            ev.RegistrationApiId = Constants.ApiConstants.NULL_ID;
                            ev.RegistrationRaceId = Constants.ApiConstants.NULL_EVENT_ID;
                            ev.RegistrationCommentFields = "";
                        }
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring apiLock. {e.Message}");
                throw new ChronokeepLockException("apiLock");
            }
        }

        public void UpdateApi(ApiObject anApi)
        {
            Log.D("MemStore", "UpdateAPI");
            database.UpdateApi(anApi);
            try
            {
                memStoreLock.Enter();
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
                Log.D("MemStore", $"Exception acquiring apiLock. {e.Message}");
                throw new ChronokeepLockException("apiLock");
            }
        }

        /**
         * Registration (Runsignup) distance map: read and written straight through, not cached.
         */

        public List<RegistrationDistance> GetRegistrationDistances(int eventId)
        {
            Log.D("MemStore", "GetRegistrationDistances");
            return database.GetRegistrationDistances(eventId);
        }

        public void SetRegistrationDistances(int eventId, List<RegistrationDistance> distances)
        {
            Log.D("MemStore", "SetRegistrationDistances");
            database.SetRegistrationDistances(eventId, distances);
        }
    }
}
