using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.Timing;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepRemote;
using Chronokeep.Timing.Announcer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Chronokeep.Timing.Remote
{
    public class RemoteReadsController(IMainWindow mainWindow, IdbInterface database) : IRemoteReadersChangeSubscriber
    {
        private static readonly Lock RemRLock = new();
        private static readonly Semaphore Waiter = new(0, 1);

        private static bool running;
        private static bool keepAlive = true;
        private static bool updateReaders = true;

        private const int SLEEP_SECONDS = 30;

        public int Errors { get; private set; }
        private readonly Dictionary<RemoteReader, DateTime> lastReaderTime = [];
        private readonly Dictionary<RemoteReader, long> remoteNotificationDictionary = [];

        public enum RemoteStatus { UNKNOWN, RUNNING, STOPPED }

        public static RemoteStatus IsRunning()
        {
            RemoteStatus output = RemoteStatus.UNKNOWN;
            if (!RemRLock.TryEnter(100)) return output;
            try
            {
                output = running ? RemoteStatus.RUNNING : RemoteStatus.STOPPED;
            }
            finally
            {
                RemRLock.Exit();
            }
            return output;
        }

        public static void Shutdown()
        {
            if (!RemRLock.TryEnter(6000)) return;
            try
            {
                Log.D("API.RemoteReadsController", "Shutting down API Auto Upload.");
                keepAlive = false;
                Waiter.Release();
            }
            finally
            {
                RemRLock.Exit();
            }
        }

        public async void Run()
        {
            try
            {
                Log.D("API.RemoteReadsController", "RemoteReadsController is now running.");
                if (RemRLock.TryEnter(6000))
                {
                    try
                    {
                        if (running)
                        {
                            Log.D("API.RemoteReadsController", "RemoteReadsController is already running.");
                            return;
                        }
                        running = true;
                        keepAlive = true;
                    }
                    finally
                    {
                        RemRLock.Exit();
                    }
                }
                else
                {
                    Log.D("API.RemoteReadsController", "Unable to acquire lock.");
                    return;
                }
                mainWindow.UpdateTimingFromController();
                // keep looping until told to stop
                Dictionary<int, ApiObject> apiDictionary = [];
                List<RemoteReader> readers = [];
                // Subscribe to reader changes.
                RemoteReadersNotifier.GetRemoteReadersNotifier().Subscribe(this);
                while (true)
                {
                    // check if we need to update our list of readers
                    if (RemRLock.TryEnter(3000))
                    {
                        try
                        {
                            if (updateReaders)
                            {
                                Log.D("API.RemoteReadsController", "Updating readers.");
                                Event theEvent = database.GetCurrentEvent()!;
                                readers = database.GetRemoteReaders(theEvent.Identifier);
                                apiDictionary.Clear();
                                foreach (ApiObject api in database.GetAllApi().Where(api => api.Type == Constants.ApiConstants.CHRONOKEEP_REMOTE || api.Type == Constants.ApiConstants.CHRONOKEEP_REMOTE_SELF))
                                {
                                    apiDictionary[api.Identifier] = api;
                                }
                                updateReaders = false;
                            }
                        }
                        finally
                        {
                            RemRLock.Exit();
                        }
                    }
                    // don't query if we just started
                    DateTime now = DateTime.Now;
                    // Start will start out at the start of the current day for each reader
                    // It will be changed based upon the last time value a reader sent us
                    DateTime end = new(now.Year, now.Month, now.Day, 23, 59, 59);
                    bool apiError = false;
                    bool announcerNotify = false;
                    foreach (RemoteReader reader in readers)
                    {
                        if (reader.LocationId == Constants.Timing.LOCATION_ANNOUNCER)
                        {
                            announcerNotify = true;
                        }
                        // make sure we know how to check the api
                        if (!apiDictionary.TryGetValue(reader.ApiiDentifier, out ApiObject? api)) continue;
                        // reset start to the start of the day each loop
                        DateTime dateTime = new(now.Year, now.Month, now.Day, 0, 0, 0);
                        DateTime start = dateTime;
                        if (lastReaderTime.TryGetValue(reader, out DateTime lastTime))
                        {
                            // query 1 second before just in case the reader didn't send us everything they had
                            // due to really good timing on our part
                            start = lastTime.AddSeconds(-1);
                        }
                        List<ChipRead> reads = [];
                        try
                        {
                            (reads, RemoteNotification note) = await api.GetReads(reader, start, end);
                            if (!(remoteNotificationDictionary.TryGetValue(reader, out long noteId)
                                  && noteId == note.Id))
                            {
                                mainWindow.ShowNotificationDialog(reader.Name, "Remote", note);
                                remoteNotificationDictionary[reader] = note.Id;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.E("API.RemoteReadsController", "Unable to handle API response - " + ex.Message);
                            apiError = true;
                        }
                        foreach (ChipRead read in reads)
                        {
                            // we want to keep track of the last reader the reader recorded so we don't request
                            // a time period we've already requested.
                            if (lastReaderTime.TryGetValue(reader, out DateTime lTime) && lTime >= read.Time)
                                continue;
                            lTime = read.Time;
                            lastReaderTime[reader] = lTime;
                        }
                        database.AddChipReads(reads);
                    }
                    if (announcerNotify)
                    {
                        AnnouncerWorker.Notify();
                    }
                    if (apiError)
                    {
                        Errors += 1;
                    }
                    else if (Errors > 0)
                    {
                        Errors = 0;
                    }
                    mainWindow.UpdateTimingFromController();
                    // wait for our sleep period
                    Log.D("API.RemoteReadsController", "Waiting to download more reads.");
                    int sleepFor = Globals.DownloadInterval;
                    if (sleepFor is < 1 or > 60)
                    {
                        sleepFor = SLEEP_SECONDS;
                    }
                    // Block with timeout on a semaphore
                    // Use this to allow us to only send information every so often based upon a global
                    // interval set, or the SleepSeconds value if the global value isn't in the correct range.
                    // We could check for if we've been signaled, but we're only signaled if we're
                    // told to exit, so we can just check KeepAlive after.
                    Waiter.WaitOne(sleepFor * 1000);
                    // check if we should exit the loop
                    if (RemRLock.TryEnter(6000))
                    {
                        try
                        {
                            Log.D("API.RemoteReadsController", "Checking keep alive status.");
                            if (!keepAlive)
                            {
                                Log.D("API.RemoteReadsController", "Exiting RemoteReads thread.");
                                running = false;
                                mainWindow.UpdateTimingFromController();
                                RemoteReadersNotifier.GetRemoteReadersNotifier().Unsubscribe(this);
                                return;
                            }
                        }
                        finally
                        {
                            RemRLock.Exit();
                        }
                    }
                    else
                    {
                        Log.D("API.RemoteReadsController", "Error with RemoteReads lock.");
                        keepAlive = false;
                        running = false;
                        mainWindow.UpdateTimingFromController();
                        RemoteReadersNotifier.GetRemoteReadersNotifier().Unsubscribe(this);
                        return;
                    }
                }
            }
            catch (Exception)
            {
                Log.D("API.RemoteReadsController", "Error running remote read fetcher.");
            }
        }

        public void NotifyRemoteReadersChange()
        {
            if (!RemRLock.TryEnter(3000)) return;
            try
            {
                updateReaders = true;
            }
            finally
            {
                RemRLock.Exit();
            }
        }
    }
}
