using Chronokeep.Helpers;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepRemote;
using Chronokeep.Test.Database;
using Chronokeep.Test.Timing;
using Chronokeep.Timing.Remote;
using System.Net;
using System.Text.Json;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Network
{
    /// <summary>
    /// Runs the remote reads download loop against a fake remote API. The loop is a background
    /// thread with static state, so each test starts it, waits for its requests, then shuts it down.
    /// </summary>
    public class RemoteReadsControllerTests : DatabaseTestBase
    {
        private readonly FakeApiServer server = new();
        private readonly Event theEvent;
        private readonly FakeMainWindow window;
        private readonly int previousInterval = Globals.DownloadInterval;
        // Two reads 10 seconds apart, at 8:00 this morning.
        private readonly long firstRead = TimingConstants.UnixDateToEpoch(DateTime.Today.AddHours(8).ToUniversalTime());

        public RemoteReadsControllerTests()
        {
            Globals.DownloadInterval = 1;
            theEvent = AddEvent(makeCurrent: true);
            int apiId = Db.AddApi(new ApiObject(0, Constants.ApiConstants.CHRONOKEEP_REMOTE, server.Url, "Remote", "token", ""));
            Db.AddRemoteReaders(theEvent.Identifier, [new RemoteReader("Finish", apiId, TimingConstants.LOCATION_FINISH, theEvent.Identifier)]);
            FakeMainWindow.Create(out window);
            server.Responder = _ => (HttpStatusCode.OK, ReadsJson());
        }

        public override void Dispose()
        {
            RemoteReadsController.Shutdown();
            WaitFor(() => RemoteReadsController.IsRunning() == RemoteReadsController.RemoteStatus.STOPPED, 10);
            Globals.DownloadInterval = previousInterval;
            server.Dispose();
            base.Dispose();
        }

        private string ReadsJson(string notification = "") =>
            $"{{\"count\":2,\"reads\":[" +
            $"{{\"identifier\":\"CHIP1\",\"seconds\":{firstRead},\"milliseconds\":0,\"ident_type\":\"chip\",\"type\":\"reader\"}}," +
            $"{{\"identifier\":\"CHIP2\",\"seconds\":{firstRead + 10},\"milliseconds\":0,\"ident_type\":\"chip\",\"type\":\"reader\"}}]" +
            (notification.Length > 0 ? $",\"notification\":{notification}" : "") + "}";

        private static bool WaitFor(Func<bool> condition, int seconds)
        {
            DateTime until = DateTime.Now.AddSeconds(seconds);
            while (DateTime.Now < until)
            {
                if (condition()) return true;
                Thread.Sleep(50);
            }
            return condition();
        }

        /// <summary>Starts the download loop the same way the app's Start Remote does.</summary>
        private RemoteReadsController Start()
        {
            RemoteReadsController controller = new((Chronokeep.Interfaces.UI.IMainWindow)(object)window, Db);
            new Thread(controller.Run) { IsBackground = true }.Start();
            return controller;
        }

        private int ReadRequests() { lock (server.Requests) return server.Requests.Count(r => r.Path == "reads"); }

        private void Stop()
        {
            RemoteReadsController.Shutdown();
            Assert.True(WaitFor(() => RemoteReadsController.IsRunning() == RemoteReadsController.RemoteStatus.STOPPED, 10));
        }

        [Fact]
        public void Downloads_ReadsIntoTheEvent()
        {
            Start();
            Assert.True(WaitFor(() => Db.GetChipReadsSafemode(theEvent.Identifier).Count == 2, 10));
            Stop();

            var reads = Db.GetChipReadsSafemode(theEvent.Identifier).OrderBy(r => r.ChipNumber).ToList();
            Assert.Equal(["CHIP1", "CHIP2"], reads.Select(r => r.ChipNumber));
            Assert.All(reads, r => Assert.Equal(TimingConstants.LOCATION_FINISH, r.LocationId));
            var first = JsonDocument.Parse(server.Requests.First(r => r.Path == "reads").Body).RootElement;
            Assert.Equal("Finish", first.GetProperty("reader").GetString());
            Assert.Equal(TimingConstants.UnixDateToEpoch(DateTime.Today.ToUniversalTime()), first.GetProperty("start").GetInt64());
        }

        [Fact]
        public void NextRequest_StartsJustBeforeTheLastRead()
        {
            Start();
            Assert.True(WaitFor(() => ReadRequests() >= 2, 10));
            Stop();

            var second = JsonDocument.Parse(server.Requests.Where(r => r.Path == "reads").ElementAt(1).Body).RootElement;
            Assert.Equal(firstRead + 10 - 1, second.GetProperty("start").GetInt64());
        }

        [Fact]
        public void ApiError_IsCounted()
        {
            server.Responder = _ => (HttpStatusCode.InternalServerError, "{\"message\":\"down\"}");
            var controller = Start();
            Assert.True(WaitFor(() => controller.Errors > 0, 10));
            Stop();
        }

        [Fact]
        public void Notification_IsShownOncePerId()
        {
            server.Responder = _ => (HttpStatusCode.OK, ReadsJson("{\"id\":5,\"type\":\"UPS_ON_BATTERY\",\"when\":\"\"}"));
            Start();
            Assert.True(WaitFor(() => ReadRequests() >= 3, 15));
            Stop();

            lock (window.Calls)
            {
                Assert.Equal(1, window.Calls.Count(c => c == "ShowNotificationDialog"));
            }
        }

        [Fact]
        public void Restarting_StillDownloadsReads()
        {
            Start();
            Assert.True(WaitFor(() => ReadRequests() >= 1, 10));
            Stop();
            lock (server.Requests) server.Requests.Clear();

            // Same as Stop Remote then Start Remote in the app: nothing else changed.
            Start();
            Assert.True(WaitFor(() => ReadRequests() >= 1, 5), "the restarted controller never asked for reads");
            Stop();
        }
    }

    public class ChronoclockTests : IDisposable
    {
        private readonly FakeApiServer server = new();
        private readonly Chronoclock clock;

        public ChronoclockTests()
        {
            clock = new Chronoclock { Name = "Finish", Url = server.Url.Replace("http://", "").TrimEnd('/') };
            server.ResponseBody = "{\"brightness\":5,\"flipDisplay\":true,\"twelveHour\":false,\"lockCountUpDown\":false,\"countupdownTimestamp\":1781539200}";
        }

        public void Dispose()
        {
            server.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task StartAndStop_UseGetAndParseState()
        {
            var started = await clock.StartCountUp();
            await clock.StopCountUp();

            Assert.Equal([("GET", "start"), ("GET", "stop")], server.Requests.Select(r => (r.Method, r.Path)));
            Assert.Equal(1781539200, started.CountUpDownTimestamp);
            Assert.True(started.FlipDisplay);
            Assert.Equal(5u, started.Brightness);
        }

        [Theory]
        [InlineData(30, "add_seconds", "seconds=30")]
        [InlineData(-45, "remove_seconds", "seconds=45")]
        public async Task AdjustTime_PostsPositiveSecondsToTheRightEndpoint(int seconds, string path, string body)
        {
            await clock.AdjustTime(seconds);
            Assert.Equal(("POST", path, body), (server.Single().Method, server.Single().Path, server.Single().Body));
        }

        [Fact]
        public async Task Settings_PostValueField()
        {
            await clock.SetBrightness(7);
            await clock.SetFlipDisplay(true);
            await clock.SetTwelveHour(false);
            await clock.SetLockCountUpDown(true);

            Assert.Equal(
                [("set_brightness", "value=7"), ("set_flip", "value=1"), ("set_twelvehour", "value=0"), ("set_lock", "value=1")],
                server.Requests.Select(r => (r.Path, r.Body)));
        }

        [Fact]
        public async Task SetTime_PostsNineteenCharacterDateTime()
        {
            server.ResponseBody = "{\"time\":\"2026-06-15 08:05:03\"}";

            var response = await clock.SetTime(new DateTime(2026, 6, 15, 8, 5, 3));

            Assert.Equal("set_time", server.Single().Path);
            Assert.Equal("DateTime=2026-06-15+08%3A05%3A03", server.Single().Body);
            Assert.Equal("2026-06-15 08:05:03", response.Time);
        }

        [Fact]
        public async Task ErrorFromClock_IsThrownWithItsMessage()
        {
            server.Status = HttpStatusCode.Conflict;
            server.ResponseBody = "{\"error\":\"CountUpDown locked.\"}";

            var ex = await Assert.ThrowsAsync<ApiException>(() => clock.AdjustTime(10));

            Assert.Contains("CountUpDown locked.", ex.Message);
        }

        [Fact]
        public async Task MissingUrl_FailsWithoutCallingTheClock()
        {
            var noUrl = new Chronoclock { Name = "Nowhere" };
            var ex = await Assert.ThrowsAsync<ApiException>(() => noUrl.StartCountUp());
            Assert.Equal("url not set", ex.Message);
            Assert.Empty(server.Requests);
        }
    }
}
