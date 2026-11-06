using Chronokeep.Objects;
using Chronokeep.Test.Database;
using Chronokeep.Timing.API;
using System.Net;
using System.Text.Json;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Network
{
    /// <summary>
    /// Runs ApiController.UploadResults (what the upload loop calls) against a fake results API
    /// and checks what is sent and which results end up marked as uploaded.
    /// </summary>
    public class ResultsUploadTests : DatabaseTestBase
    {
        private readonly FakeApiServer server = new();
        private readonly ApiObject api;
        private Event theEvent = null!;
        private int nextBib = 1;

        public ResultsUploadTests()
        {
            api = new ApiObject(1, Constants.ApiConstants.CHRONOKEEP_RESULTS, server.Url, "Test", "token", "");
            Db.SetAppSetting(Constants.Settings.PROGRAM_UNIQUE_MODIFIER, "XYZ");
            // By default the server accepts everything it's sent.
            server.Responder = request => (HttpStatusCode.OK, $"{{\"count\":{SentCount(request)}}}");
        }

        public override void Dispose()
        {
            server.Dispose();
            base.Dispose();
        }

        private static int SentCount(FakeApiServer.Request request)
            => JsonDocument.Parse(request.Body).RootElement.GetProperty("results").GetArrayLength();

        private void Setup(Action<Event>? configure = null)
        {
            theEvent = new Event { Name = "Race", Date = DateString(), StartSeconds = 8 * 3600 };
            configure?.Invoke(theEvent);
            theEvent.Identifier = Db.AddEvent(theEvent);
            Db.UpdateEvent(theEvent);
            theEvent = Db.GetEvent(theEvent.Identifier)!;
        }

        private Distance Distance(string name, bool upload = true, int waveOffset = 0)
        {
            Distance d = new(name, theEvent.Identifier) { Upload = upload, StartOffsetSeconds = waveOffset };
            d.Identifier = Db.AddDistance(d);
            Db.UpdateDistance(d);
            return d;
        }

        /// <summary>Adds runners in a distance, each with a finish result.</summary>
        private void Finishers(Distance d, int count, int status = TimingConstants.TIMERESULT_STATUS_NONE)
        {
            for (int i = 0; i < count; i++)
            {
                string bib = (nextBib++).ToString();
                var p = AddParticipant(theEvent.Identifier, d, bib, first: $"Runner{bib}");
                Db.AddTimingResult(new TimeResult(theEvent.Identifier, TimingConstants.TIMERESULT_DUMMYREAD, p.EventSpecific.Identifier,
                    TimingConstants.LOCATION_FINISH, TimingConstants.SEGMENT_FINISH, 1, 1500 + i, 0, TimeResult.BibToIdentifier(bib),
                    1500 + i, 0, DateTime.Now, bib, status, ""));
            }
        }

        private Task Upload() => ApiController.UploadResults(
            Db.GetNonUploadedResults(theEvent.Identifier), api, ["race", "2026"], Db, null, null, theEvent);

        private List<string> SentBibs() => [.. server.Requests.SelectMany(r =>
            JsonDocument.Parse(r.Body).RootElement.GetProperty("results").EnumerateArray().Select(x => x.GetProperty("bib").GetString()!))];

        [Fact]
        public async Task UploadsInBatchesOfTwenty_AndMarksEverythingUploaded()
        {
            Setup();
            Finishers(Distance("5K"), 45);

            await Upload();

            Assert.Equal([20, 20, 5], server.Requests.Select(SentCount));
            Assert.All(server.Requests, r => Assert.Equal("results/add", r.Path));
            Assert.Empty(Db.GetNonUploadedResults(theEvent.Identifier));
        }

        [Fact]
        public async Task NothingLeft_SendsNothing()
        {
            Setup();
            Finishers(Distance("5K"), 3);
            await Upload();
            server.Requests.Clear();

            await Upload();

            Assert.Empty(server.Requests);
        }

        [Fact]
        public async Task ServerError_StopsAndRetriesLater()
        {
            Setup();
            Finishers(Distance("5K"), 25);
            int calls = 0;
            server.Responder = request => ++calls == 2
                ? (HttpStatusCode.InternalServerError, "{\"message\":\"boom\"}")
                : (HttpStatusCode.OK, $"{{\"count\":{SentCount(request)}}}");

            await Upload();

            Assert.Equal(5, Db.GetNonUploadedResults(theEvent.Identifier).Count);

            // The upload flag was released, so the next pass sends the rest.
            await Upload();
            Assert.Empty(Db.GetNonUploadedResults(theEvent.Identifier));
        }

        [Fact]
        public async Task PartialAcceptance_LeavesBatchForRetry()
        {
            Setup();
            Finishers(Distance("5K"), 3);
            server.Responder = _ => (HttpStatusCode.OK, "{\"count\":2}");

            await Upload();

            Assert.Equal(3, Db.GetNonUploadedResults(theEvent.Identifier).Count);
        }

        [Fact]
        public async Task UploadSpecificDistances_OnlySendsSelectedDistances()
        {
            Setup(e => e.UploadSpecific = true);
            Finishers(Distance("5K", upload: true), 3);
            Finishers(Distance("10K", upload: false), 2);

            await Upload();

            Assert.Equal(["1", "2", "3"], SentBibs().Order());
        }

        [Fact]
        public async Task UploadSpecificDistances_DoesNotResendForever()
        {
            Setup(e => e.UploadSpecific = true);
            Finishers(Distance("5K", upload: true), 3);
            Finishers(Distance("10K", upload: false), 2);

            await Upload();
            server.Requests.Clear();
            await Upload();

            Assert.Empty(SentBibs());
        }

        [Fact]
        public async Task SkippedDistance_IsUploadedOnceItIsSelected()
        {
            Setup(e => e.UploadSpecific = true);
            Finishers(Distance("5K", upload: true), 2);
            var tenK = Distance("10K", upload: false);
            Finishers(tenK, 2);
            await Upload();
            server.Requests.Clear();

            tenK.Upload = true;
            Db.UpdateDistance(tenK);
            await Upload();

            Assert.Equal(["3", "4"], SentBibs().Order());
            Assert.Empty(Db.GetNonUploadedResults(theEvent.Identifier));
        }

        [Fact]
        public async Task BatchWithNothingToSend_MakesNoRequest()
        {
            Setup(e => e.UploadSpecific = true);
            Finishers(Distance("10K", upload: false), 25);

            await Upload();

            Assert.Empty(server.Requests);
        }

        [Fact]
        public async Task BackyardUltra_DoesNotSendDnfs_AndDoesNotResendForever()
        {
            Setup(e => e.EventType = TimingConstants.EVENT_TYPE_BACKYARD_ULTRA);
            var d = Distance("Backyard");
            Finishers(d, 2);
            Finishers(d, 1, TimingConstants.TIMERESULT_STATUS_DNF);

            await Upload();
            Assert.Equal(["1", "2"], SentBibs().Order());

            server.Requests.Clear();
            await Upload();
            Assert.Empty(SentBibs());
        }

        [Fact]
        public async Task LocalTime_UsesTheDistanceWaveStart()
        {
            Setup();
            Finishers(Distance("Wave 2", waveOffset: 600), 1);

            await Upload();

            var result = JsonDocument.Parse(server.Single().Body).RootElement.GetProperty("results")[0];
            DateTime expected = DateTime.Parse(theEvent.Date).AddSeconds(8 * 3600 + 600 + 1500);
            Assert.Equal(expected, DateTime.Parse(result.GetProperty("local_time").GetString()!));
            Assert.Equal("XYZ", result.GetProperty("person_id").GetString()!.Split('-')[1]);
        }
    }
}
