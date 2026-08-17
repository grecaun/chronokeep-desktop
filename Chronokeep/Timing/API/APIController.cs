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

using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Chronokeep.Timing.API
{
    internal class ApiController(IMainWindow mainWindow, IdbInterface database)
    {
        private static readonly Lock ApiLock = new();
        private static readonly Semaphore Waiter = new(0, 1);
        private static bool canUpload = true;
        private static bool isUploading;
        private static bool running;
        private static bool keepAlive = true;

        public int Errors { get; private set; }

        private const int SleepSeconds = 30;

        public static async Task DeleteResults(ApiObject api, string slug, string year, string? distance)
        {
            try
            {
                AddResultsResponse? response;
                if (distance is { Length: > 0 })
                {
                    response = await ApiHandlers.DeleteDistanceResults(api, slug, year, distance);
                }
                else
                {
                    response = await ApiHandlers.DeleteResults(api, slug, year);
                }

                Log.D("API.APIController", $"API Controller response: {response.Count}");
            }
            catch
            {
                Log.D("API.APIController", "Error deleting results.");
            }
        }

        public static async Task UploadResults(
            List<TimeResult> results,
            ApiObject api,
            string[] eventIds,
            IdbInterface database,
            ApiController? controller,
            IMainWindow? mainWindow,
            Event theEvent
            )
        {
            DateTime start = DateTime.SpecifyKind(DateTime.Parse(theEvent.Date), DateTimeKind.Local).AddSeconds(theEvent.StartSeconds).AddMilliseconds(theEvent.StartMilliseconds);
            Dictionary<string, DateTime> waveStartTimes = [];
            HashSet<string> uploadDistances = [];
            foreach (Distance d in database.GetDistances(theEvent.Identifier))
            {
                waveStartTimes[d.Name] = start.AddSeconds(d.StartOffsetSeconds).AddMilliseconds(d.StartOffsetMilliseconds);
                if (d is { Upload: true, LinkedDistance: Constants.Timing.DISTANCE_NO_LINKED_ID })
                {
                    uploadDistances.Add(d.Name);
                }
            }
            AppSetting uniqueId = database.GetAppSetting(Constants.Settings.PROGRAM_UNIQUE_MODIFIER)!;
            string uniquePad = uniqueId.Value;
            Log.D("API.APIController", $"Attempting to upload {results.Count} results.");
            if (ApiLock.TryEnter(3000))
            {
                try
                {
                    if (isUploading)
                    {
                        return;
                    }
                    isUploading = true;
                }
                finally
                {
                    ApiLock.Exit();
                }
            }
            else
            {
                throw new Exception("error grabbing lock to signal start");
            }
            int total = 0;
            int loops = results.Count / Constants.Timing.API_LOOP_COUNT;
            AddResultsResponse? response;
            bool loopError = false;
            for (int i = 0; i < loops; i += 1)
            {
                Log.D("API.APIController", $"Loop {i}");
                // Change TimeResults to APIResults - breaking this up into chunks so we can
                // properly update them with the UPLOADED field
                List<ApiResult> upRes = [];
                List<TimeResult> uploaded = results.GetRange(i * Constants.Timing.API_LOOP_COUNT, Constants.Timing.API_LOOP_COUNT);
                foreach (TimeResult tr in uploaded)
                {
                    //tr.Uploaded = Constants.Timing.TIMERESULT_UPLOADED_TRUE;
                    DateTime trStart = waveStartTimes.GetValueOrDefault(tr.RealDistanceName, start);
                    // only add to upload list if we want to upload everything (NOT Specific)
                    // or we only want to upload specific distances and the distance is in the
                    // list of distances we want to upload
                    if (Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA != theEvent.EventType && (!theEvent.UploadSpecific || uploadDistances.Contains(tr.DistanceName)))
                    {
                        upRes.Add(new ApiResult(theEvent, tr, trStart, uniquePad));
                    }
                    // Make sure that DNF entries are not uploaded when timing Backyard Ultra since multiples are generated and
                    // that info isn't useful to others
                    else if (Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA == theEvent.EventType && Constants.Timing.TIMERESULT_STATUS_DNF != tr.Status)
                    {
                        upRes.Add(new ApiResult(theEvent, tr, trStart, uniquePad));
                    }
                }
                try
                {
                    response = await ApiHandlers.UploadResults(api, eventIds[0], eventIds[1], upRes);
                }
                catch
                {
                    // Error uploading due to network issues most likely. Keep tally of these errors but continue running.
                    Log.D("API.APIController", $"Unable to handle API response. Loop {i}");
                    loopError = true;
                    controller?.Errors += 1;
                    mainWindow?.UpdateTiming();
                    break;
                }
                total += response.Count;
                Log.D("API.APIController", $"Total: {total} Count: {response.Count}");
                if (response.Count != Constants.Timing.API_LOOP_COUNT) continue;
                // Updating uploaded value for uploaded results.
                foreach (TimeResult res in uploaded)
                {
                    res.Uploaded = Constants.Timing.TIMERESULT_UPLOADED_TRUE;
                }
                database.SetUploadedTimingResults(uploaded);
            }
            int leftovers = results.Count - (loops * Constants.Timing.API_LOOP_COUNT);
            if (leftovers > 0 && !loopError)
            {
                response = null;
                // Change TimeResults to APIResults
                List<ApiResult> upRes = [];
                List<TimeResult> uploaded = results.GetRange(loops * Constants.Timing.API_LOOP_COUNT, leftovers);
                foreach (TimeResult tr in uploaded)
                {
                    //tr.Uploaded = Constants.Timing.TIMERESULT_UPLOADED_TRUE;
                    DateTime trStart = waveStartTimes.GetValueOrDefault(tr.RealDistanceName, start);
                    // only add to upload list if we want to upload everything (NOT Specific)
                    // or we only want to upload specific distances and the distance is in the
                    // list of distances we want to upload
                    if (Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA != theEvent.EventType && (!theEvent.UploadSpecific || uploadDistances.Contains(tr.DistanceName)))
                    {
                        upRes.Add(new ApiResult(theEvent, tr, trStart, uniquePad));
                    }
                    // Make sure that DNF entries are not uploaded when timing Backyard Ultra since multiples are generated and
                    // that info isn't useful to others
                    else if (Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA == theEvent.EventType && Constants.Timing.TIMERESULT_STATUS_DNF != tr.Status)
                    {
                        upRes.Add(new ApiResult(theEvent, tr, trStart, uniquePad));
                    }
                }
                try
                {
                    response = await ApiHandlers.UploadResults(api, eventIds[0], eventIds[1], upRes);
                }
                catch
                {
                    // Error uploading due to network issues most likely. Keep tally of these errors but continue running.
                    Log.D("API.APIController", "Unable to handle API response. Leftovers");
                    loopError = true;
                    controller?.Errors += 1;
                    mainWindow?.UpdateTiming();
                }
                if (response != null)
                {
                    total += response.Count;
                    Log.D("API.APIController", $"Total: {total} Count: {response.Count}");
                    if (response.Count == leftovers)
                    {
                        // Updating uploaded value for uploaded results;
                        foreach (TimeResult res in uploaded)
                        {
                            res.Uploaded = Constants.Timing.TIMERESULT_UPLOADED_TRUE;
                        }
                        database.SetUploadedTimingResults(uploaded);
                    }
                }
                Log.D("API.APIController", $"Upload finished. Count total: {total}");
            }
            if (!loopError && controller != null)
            {
                controller.Errors = 0;
            }
            if (ApiLock.TryEnter(3000))
            {
                try
                {
                    isUploading = false;
                }
                finally
                {
                    ApiLock.Exit();
                }
            }
            else
            {
                throw new Exception("error grabbing lock to signal completion");
            }
        }

        public static bool SetUploadableTrue(int millisecondsTimeout)
        {
            if (!ApiLock.TryEnter(millisecondsTimeout)) return false;
            try
            {
                canUpload = true;
            }
            finally
            {
                ApiLock.Exit();
            }
            return true;
        }

        public static bool SetUploadableFalse(int millisecondsTimeout)
        {
            if (!ApiLock.TryEnter(millisecondsTimeout)) return false;
            try
            {
                canUpload = false;
            }
            finally
            {
                ApiLock.Exit();
            }
            return true;
        }

        public static bool GetUploadable(int millisecondsTimeout)
        {
            bool output = false;
            if (!ApiLock.TryEnter(millisecondsTimeout)) return output;
            try
            {
                output = canUpload;
            }
            finally
            {
                ApiLock.Exit();
            }
            return output;
        }

        public static bool IsUploading()
        {
            bool output = true;
            if (!ApiLock.TryEnter(3000)) return output;
            try
            {
                output = isUploading;
            }
            finally
            {
                ApiLock.Exit();
            }
            return output;
        }

        public static bool IsRunning()
        {
            bool output = false;
            if (!ApiLock.TryEnter(6000)) return output;
            try
            {
                output = running;
            }
            finally
            {
                ApiLock.Exit();
            }
            return output;
        }

        public static void Shutdown()
        {
            if (!ApiLock.TryEnter(6000)) return;
            try
            {
                Log.D("API.APIController", "Shutting down API Auto Upload.");
                keepAlive = false;
                Waiter.Release();
            }
            finally
            {
                ApiLock.Exit();
            }
        }

        public async void Run()
        {
            try
            {
                Log.D("API.APIController", "API Controller is now running.");
                if (ApiLock.TryEnter(6000))
                {
                    try
                    {
                        if (running)
                        {
                            Log.D("API.APIController", "API Controller thread is already running.");
                            return;
                        }
                        running = true;
                        keepAlive = true;
                    }
                    finally
                    {
                        ApiLock.Exit();
                    }
                }
                else
                {
                    Log.D("API.APIController", "Unable to acquire lock.");
                    return;
                }
                mainWindow.UpdateTiming();
                // keep looping until told to stop
                while (true)
                {
                    // Start upload of data to API.
                    Event theEvent = database.GetCurrentEvent()!;
                    // Get API to upload. Exit if not found
                    if (theEvent is { ApiId: < 0, ApiEventId.Length: > 1 })
                    {
                        Log.D("API.APIController", "Unable to find API information.");
                        keepAlive = false;
                        running = false;
                        mainWindow.UpdateTiming();
                        return;
                    }
                    ApiObject api;
                    try
                    {
                        api = database.GetApi(theEvent.ApiId)!;
                    }
                    catch
                    {
                        Log.D("API.APIController", "Database doesn't contain information about the specified API.");
                        keepAlive = false;
                        running = false;
                        mainWindow.UpdateTiming();
                        return;
                    }
                    // Get the event id values. Exit if not valid.
                    string[] eventIds = theEvent.ApiEventId.Split(',');
                    if (eventIds.Length != 2)
                    {
                        Log.D("API.APIController", "Event ID values for API upload not valid.");
                        keepAlive = false;
                        running = false;
                        mainWindow.UpdateTiming();
                        return;
                    }
                    // Get results to upload.
                    List<TimeResult> results = database.GetNonUploadedResults(theEvent.Identifier);
                    // Remove all results to upload that don't have a place set, are not DNF/DNS results, and are also not start times.
                    results.RemoveAll(x => x.Place < 1
                                           && x.Status != Constants.Timing.TIMERESULT_STATUS_DNF
                                           && x.Status != Constants.Timing.TIMERESULT_STATUS_DNS
                                           && x.SegmentId != Constants.Timing.SEGMENT_START);
                    Log.D("API.APIController", $"Results count: {results.Count}");
                    bool upload = false;
                    if (ApiLock.TryEnter(3000))
                    {
                        try
                        {
                            upload = canUpload;
                        }
                        finally
                        {
                            ApiLock.Exit();
                        }
                    }
                    //Log.D("Timing.API.APIController", $"We are {(!upload ? "not " : "")}able to upload right now.");
                    if (results.Count > 0 && upload)
                    {
                        await UploadResults(results, api, eventIds, database, this, mainWindow, theEvent);
                        mainWindow.UpdateTiming();
                    }
                    else // KeepAlive check
                    {
                        try
                        {
                            bool healthy = await ApiHandlers.IsHealthy(api);
                            // clear errors if we don't get an exception
                            if (healthy)
                            {
                                Errors = 0;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.D("API.APIController", ex.Message);
                            Errors += 1;
                            mainWindow.UpdateTiming();
                        }
                    }
                    // Block with timeout on a semaphore
                    // Use this to allow us to only send information every so often based upon a global
                    // interval set, or the SleepSeconds value if the global value isn't in the correct range.
                    // We could check for if we've been signaled, but we're only signaled if we're
                    // told to exit, so we can just check KeepAlive after.
                    Log.D("API.APIController", "Waiting to upload more results.");
                    int sleepFor = Globals.UploadInterval;
                    if (sleepFor is < 1 or > 60)
                    {
                        sleepFor = SleepSeconds;
                    }
                    Waiter.WaitOne(sleepFor * 1000);
                    // Check if we're supposed to exit the loop
                    if (ApiLock.TryEnter(6000))
                    {
                        try
                        {
                            Log.D("API.APIController", "Checking keep alive status.");
                            if (!keepAlive)
                            {
                                Log.D("API.APIController", "Exiting API thread.");
                                running = false;
                                mainWindow.UpdateTiming();
                                return;
                            }
                        }
                        finally
                        {
                            ApiLock.Exit();
                        }
                    }
                    else
                    {
                        Log.D("API.APIController", "Error with API lock.");
                        keepAlive = false;
                        running = false;
                        mainWindow.UpdateTiming();
                        return;
                    }
                }
            }
            catch (Exception)
            {
                Log.D("API.APIController", "Error running api controller.");
            }
        }
    }
}

