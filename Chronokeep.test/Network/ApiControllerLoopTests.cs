using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.Test.Database;
using Chronokeep.Test.Timing;
using Chronokeep.Timing.API;
using System.Net;
using System.Reflection;
using System.Text.Json;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Network
{
    /// <summary>
    /// The automatic upload loop (ApiController.Run): it uploads placed results every interval,
    /// checks the API's health when there's nothing to send, and stops when told to.
    /// </summary>
    public class ApiControllerLoopTests : DatabaseTestBase
    {
        private readonly FakeApiServer server = new();
        private readonly IMainWindowCalls window;
        private readonly int savedInterval = Globals.UploadInterval;
        private Event theEvent = null!;

        /// <summary>Wraps the routine tests' recording window.</summary>
        private sealed class IMainWindowCalls(Interfaces.UI.IMainWindow window, FakeMainWindow fake)
        {
            public Interfaces.UI.IMainWindow Window { get; } = window;
            public FakeMainWindow Fake { get; } = fake;
        }

        public ApiControllerLoopTests()
        {
            window = new(FakeMainWindow.Create(out FakeMainWindow fake), fake);
            Db.SetAppSetting(Constants.Settings.PROGRAM_UNIQUE_MODIFIER, "XYZ");
            Globals.UploadInterval = 1;
            ResetStatics();
            server.Responder = request => request.Path switch
            {
                "results/add" => (HttpStatusCode.OK, $"{{\"count\":{JsonDocument.Parse(request.Body).RootElement.GetProperty("results").GetArrayLength()}}}"),
                _ => (HttpStatusCode.OK, "{}"),
            };
        }

        public override void Dispose()
        {
            ApiController.Shutdown();
            WaitFor(() => !ApiController.IsRunning());
            ResetStatics();
            Globals.UploadInterval = savedInterval;
            server.Dispose();
            base.Dispose();
        }

        /// <summary>The loop keeps its state in statics; start each test from a stopped, uploadable controller.</summary>
        private static void ResetStatics()
        {
            Type type = typeof(ApiController);
            type.GetField("running", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
            type.GetField("keepAlive", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, true);
            type.GetField("isUploading", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
            ApiController.SetUploadableTrue(1000);
            Semaphore waiter = (Semaphore)type.GetField("Waiter", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            while (waiter.WaitOne(0)) { }
        }

        private static bool WaitFor(Func<bool> condition, int timeoutMs = 8000)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < end)
            {
                if (condition()) return true;
                Thread.Sleep(25);
            }
            return condition();
        }

        private void LinkedEvent(bool linked = true)
        {
            theEvent = AddEvent(makeCurrent: true);
            if (linked)
            {
                theEvent.ApiId = Db.AddApi(new ApiObject(0, Constants.ApiConstants.CHRONOKEEP_RESULTS_SELF, server.Url, "Local", "tok", ""));
                theEvent.ApiEventId = "race,2026";
                Db.UpdateEvent(theEvent);
            }
            theEvent = Db.GetCurrentEvent()!;
        }

        private void PlacedFinisher(string bib, int place)
        {
            Distance d = Db.GetDistances(theEvent.Identifier).FirstOrDefault() ?? AddDistance(theEvent.Identifier);
            Participant p = AddParticipant(theEvent.Identifier, d, bib);
            TimeResult r = new(theEvent.Identifier, TimingConstants.TIMERESULT_DUMMYREAD, p.EventSpecific.Identifier,
                TimingConstants.LOCATION_FINISH, TimingConstants.SEGMENT_FINISH, 1, 1500, 0, TimeResult.BibToIdentifier(bib),
                1500, 0, DateTime.Now, bib, TimingConstants.TIMERESULT_STATUS_PROCESSED, "") { Place = place };
            Db.AddTimingResult(r);
        }

        private ApiController Start()
        {
            ApiController controller = new(window.Window, Db);
            new Thread(controller.Run) { IsBackground = true }.Start();
            Assert.True(WaitFor(ApiController.IsRunning), "The upload loop never started.");
            return controller;
        }

        private int Requests(string path) => ServerRequestsCount(path);

        private int ServerRequestsCount(string path)
        {
            lock (server.Requests)
            {
                return server.Requests.Count(r => r.Path == path);
            }
        }

        [Fact]
        public void Uploads_placed_results_and_marks_them_uploaded()
        {
            LinkedEvent();
            PlacedFinisher("1", 1);
            PlacedFinisher("2", 2);

            Start();

            Assert.True(WaitFor(() => Db.GetNonUploadedResults(theEvent.Identifier).Count == 0), "The results were never uploaded.");
            Assert.True(Requests("results/add") >= 1);
        }

        [Fact]
        public void Unplaced_results_wait_until_they_are_placed()
        {
            LinkedEvent();
            PlacedFinisher("1", TimingConstants.TIMERESULT_DUMMYPLACE);

            Start();
            Assert.True(WaitFor(() => Requests("health") >= 1), "The loop never checked the API.");

            Assert.Equal(0, Requests("results/add"));
            Assert.Single(Db.GetNonUploadedResults(theEvent.Identifier));
        }

        [Fact]
        public void Nothing_to_send_checks_the_apis_health_and_counts_failures()
        {
            LinkedEvent();
            server.Responder = _ => (HttpStatusCode.InternalServerError, """{"message":"down"}""");

            ApiController controller = Start();

            Assert.True(WaitFor(() => controller.Errors >= 2), "Health check failures weren't counted.");
            server.Responder = _ => (HttpStatusCode.OK, "{}");
            Assert.True(WaitFor(() => controller.Errors == 0), "A healthy check didn't clear the errors.");
        }

        [Fact]
        public void Uploads_pause_while_recalculating()
        {
            LinkedEvent();
            PlacedFinisher("1", 1);
            ApiController.SetUploadableFalse(1000);

            Start();
            Assert.True(WaitFor(() => Requests("health") >= 1), "The loop never ran.");
            Assert.Equal(0, Requests("results/add"));

            ApiController.SetUploadableTrue(1000);
            Assert.True(WaitFor(() => Db.GetNonUploadedResults(theEvent.Identifier).Count == 0), "Uploads didn't resume.");
        }

        [Fact]
        public void Shutdown_stops_the_loop()
        {
            LinkedEvent();
            Start();

            ApiController.Shutdown();

            Assert.True(WaitFor(() => !ApiController.IsRunning()), "The loop is still running after Shutdown.");
        }

        [Fact]
        public void A_second_start_while_running_does_nothing()
        {
            LinkedEvent();
            Start();
            int before = window.Fake.Calls.Count(c => c == "UpdateTiming");

            new ApiController(window.Window, Db).Run();

            Assert.Equal(before, window.Fake.Calls.Count(c => c == "UpdateTiming"));
            Assert.True(ApiController.IsRunning());
        }

        [Fact]
        public void An_event_without_a_linked_api_stops_at_once()
        {
            LinkedEvent(linked: false);

            new ApiController(window.Window, Db).Run();

            Assert.True(WaitFor(() => !ApiController.IsRunning()));
            Assert.Empty(server.Requests);
        }

        [Fact]
        public void A_pass_that_fails_is_counted_and_uploads_carry_on()
        {
            LinkedEvent();
            PlacedFinisher("1", 1);
            ApiController controller = new(window.Window, FailOnceDatabase.Wrap(Db, nameof(Chronokeep.Database.IdbInterface.GetNonUploadedResults)));
            new Thread(controller.Run) { IsBackground = true }.Start();

            Assert.True(WaitFor(() => Db.GetNonUploadedResults(theEvent.Identifier).Count == 0), "Uploads stopped after one failed pass.");
            Assert.True(ApiController.IsRunning());
        }

        [Fact]
        public void Without_a_current_event_the_loop_stops_and_can_start_again_later()
        {
            Db.SetCurrentEvent(-1);

            new ApiController(window.Window, Db).Run();

            Assert.True(WaitFor(() => !ApiController.IsRunning(), 2000), "The loop failed but still reports running.");
            LinkedEvent();
            PlacedFinisher("1", 1);
            Start();
            Assert.True(WaitFor(() => Db.GetNonUploadedResults(theEvent.Identifier).Count == 0), "The loop couldn't be started again.");
        }
    }
}
