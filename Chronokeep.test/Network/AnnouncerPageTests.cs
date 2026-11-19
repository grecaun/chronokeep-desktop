using Chronokeep.Helpers;
using Chronokeep.Network;
using Chronokeep.Objects;
using Chronokeep.Test.Database;
using Chronokeep.Timing.Announcer;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Network
{
    /// <summary>
    /// The announcer page on the local web server: the page itself, and the data it asks for every second.
    /// </summary>
    public class AnnouncerPageTests : DatabaseTestBase
    {
        private readonly HttpServer server;
        private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(5) };
        private readonly int port;
        private readonly Event theEvent;
        private readonly Distance fiveK;
        private readonly int previousRepeatWindow = Globals.SeenWindow;

        public AnnouncerPageTests()
        {
            AnnouncerList().Clear();
            Globals.SeenWindow = 5;
            theEvent = AddEvent(makeCurrent: true);
            fiveK = AddDistance(theEvent.Identifier);
            TcpListener l = new(IPAddress.Loopback, 0);
            l.Start();
            port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            server = new HttpServer(Db, port, "localhost");
        }

        public override void Dispose()
        {
            server.Stop();
            client.Dispose();
            AnnouncerList().Clear();
            AnnouncerParticipant.TheEvent = null;
            Globals.SeenWindow = previousRepeatWindow;
            base.Dispose();
        }

        private static IList AnnouncerList() =>
            (IList)typeof(AnnouncerWorker).GetField("Participants", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        private static void Seen(string bib, Participant? p, int secondsAgo) =>
            AnnouncerList().Add(new AnnouncerParticipant(bib, p, TimingConstants.RfidDateToEpoch(DateTime.Now.AddSeconds(-secondsAgo))));

        /// <summary>A finish result tied to a real chip read; saved results take their system time from their read.</summary>
        private void Finish(Participant p, int secondsAgo, int status = TimingConstants.TIMERESULT_STATUS_NONE)
        {
            DateTime when = DateTime.Now.AddSeconds(-secondsAgo);
            Db.AddChipReads([new ChipRead(theEvent.Identifier, TimingConstants.LOCATION_FINISH, p.Bib, when, TimingConstants.CHIPREAD_STATUS_NONE)]);
            int readId = Db.GetChipReads(theEvent.Identifier).Single(r => r.ReadBib == p.Bib).ReadId;
            Db.AddTimingResult(new TimeResult(theEvent.Identifier, readId, p.EventSpecific.Identifier,
                TimingConstants.LOCATION_FINISH, TimingConstants.SEGMENT_FINISH, 1, 1800, 0, "", 1800, 0, when, p.Bib, status, ""));
        }

        private Participant Runner(string bib, string first, bool anonymous = false, string gender = "F")
        {
            Participant? p = Db.AddParticipant(new Participant(
                first, "Runner", "123 Main St", "Springfield", "OR", "97477", "01/02/1990",
                new EventSpecific(theEvent.Identifier, fiveK.Identifier, fiveK.Name, bib, 0, $"Cheer for {first}", "", "", anonymous, false, "", ""),
                "", "", "", "", "", "", gender, "", ""));
            Assert.NotNull(p);
            return p;
        }

        private async Task<(HttpStatusCode Status, string Body, string? Type)> Get(string path)
        {
            using var response = await client.GetAsync($"http://localhost:{port}/{path}");
            return (response.StatusCode, await response.Content.ReadAsStringAsync(), response.Content.Headers.ContentType?.MediaType);
        }

        /// <summary>Asks for the announcer data, by default after telling the server the results changed.</summary>
        private async Task<JsonElement> Data(bool unknown = false, bool refresh = true)
        {
            if (refresh) server.UpdateInformation();
            var (status, body, type) = await Get($"announcer/data?unknown={(unknown ? 1 : 0)}");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("application/json", type);
            return JsonDocument.Parse(body).RootElement;
        }

        private static List<string> Bibs(JsonElement data) =>
            [.. data.GetProperty("runners").EnumerateArray().Select(r => r.GetProperty("bib").GetString()!)];

        [Fact]
        public async Task Page_IsServed()
        {
            foreach (string path in new[] { "announcer", "announcer/" })
            {
                var (status, body, type) = await Get(path);
                Assert.Equal(HttpStatusCode.OK, status);
                Assert.Equal("text/html", type);
                Assert.Contains("/announcer/data", body);
                Assert.Contains("Show unknown runners", body);
            }
        }

        [Fact]
        public async Task Data_ListsAnnouncerReadsAndFinishersOnceEachNewestFirst()
        {
            Participant passing = Runner("1", "Passing");
            Participant finished = Runner("2", "Finished");
            Participant both = Runner("3", "Both");
            Participant old = Runner("4", "Old");
            Participant dnf = Runner("5", "Dnf");
            Finish(finished, 10);
            Finish(both, 3);
            Finish(old, 1800);
            Finish(dnf, 2, TimingConstants.TIMERESULT_STATUS_DNF);
            Seen("1", passing, 5);
            Seen("3", both, 30);

            JsonElement data = await Data();

            // Each shows at the time they were first seen; Both passed the announcer before finishing.
            Assert.Equal(["1", "2", "3"], Bibs(data));
            JsonElement bothRow = data.GetProperty("runners")[2];
            Assert.Equal("Both Runner", bothRow.GetProperty("name").GetString());
            Assert.Equal("5K", bothRow.GetProperty("distance").GetString());
            Assert.Equal("Springfield OR", bothRow.GetProperty("cityState").GetString());
            Assert.Equal("Cheer for Both", bothRow.GetProperty("comments").GetString());
            // Time is the finish time, and stays blank for someone only seen at the announcer location so far.
            Assert.Equal("0:30:00", bothRow.GetProperty("time").GetString());
            Assert.Equal("", data.GetProperty("runners")[0].GetProperty("time").GetString());
            // The page shows how long ago from this, using the timing computer's clock.
            Assert.InRange(bothRow.GetProperty("at").GetInt64(),
                DateTimeOffset.Now.AddSeconds(-32).ToUnixTimeMilliseconds(), DateTimeOffset.Now.AddSeconds(-28).ToUnixTimeMilliseconds());
            Assert.Equal("2026 Test Race", data.GetProperty("event").GetString());
        }

        [Fact]
        public async Task Data_ShowsUnknownBibsOnlyWhenAsked()
        {
            Seen("1", Runner("1", "Known"), 10);
            Seen("77", null, 5);

            Assert.Equal(["1"], Bibs(await Data()));
            Assert.Equal(["77", "1"], Bibs(await Data(unknown: true)));
        }

        [Fact]
        public async Task Data_ShowsGenderAsItsShortCode()
        {
            Finish(Runner("21", "Woman", gender: "Woman"), 4);
            Finish(Runner("22", "Man", gender: "Man"), 3);
            Finish(Runner("23", "NonBinary", gender: "Non-Binary"), 2);
            Finish(Runner("24", "Unspecified", gender: "Not Specified"), 1);

            JsonElement data = await Data();
            string AgeGender(string bib) => data.GetProperty("runners").EnumerateArray()
                .Single(r => r.GetProperty("bib").GetString() == bib).GetProperty("ageGender").GetString()!;
            string age = Runner("25", "Age").Age(theEvent.Date);
            Assert.Equal($"{age} W", AgeGender("21"));
            Assert.Equal($"{age} M", AgeGender("22"));
            Assert.Equal($"{age} X", AgeGender("23"));
            // No code, and no trailing space, when the gender isn't specified.
            Assert.Equal(age, AgeGender("24"));
        }

        [Fact]
        public async Task Data_DoesNotNameAnonymousRunners()
        {
            Finish(Runner("8", "Secret", anonymous: true), 5);

            JsonElement runner = Assert.Single((await Data()).GetProperty("runners").EnumerateArray());
            Assert.Equal("8", runner.GetProperty("bib").GetString());
            Assert.Equal("Anonymous", runner.GetProperty("name").GetString());
            Assert.Equal("", runner.GetProperty("cityState").GetString());
            Assert.Equal("", runner.GetProperty("comments").GetString());
            Assert.DoesNotContain("Secret", runner.ToString());
        }

        [Fact]
        public async Task Data_HasNoClockStartsBeforeTheRaceStarts()
        {
            JsonElement clock = Assert.Single((await Data()).GetProperty("clocks").EnumerateArray());

            Assert.Equal("5K", clock.GetProperty("distance").GetString());
            Assert.Equal(JsonValueKind.Null, clock.GetProperty("start").ValueKind);
        }

        [Fact]
        public async Task Data_GivesEachDistanceItsStartTime()
        {
            Distance tenK = AddDistance(theEvent.Identifier, "10K");
            tenK.StartOffsetSeconds = 600;
            tenK.StartOffsetMilliseconds = 500;
            Db.UpdateDistance(tenK);
            theEvent.StartSeconds = 8 * 3600;
            theEvent.StartMilliseconds = 250;
            Db.UpdateEvent(theEvent);

            JsonElement data = await Data();

            DateTime gun = EventDates.Parse(theEvent.Date).AddHours(8).AddMilliseconds(250);
            Dictionary<string, long> starts = data.GetProperty("clocks").EnumerateArray()
                .ToDictionary(c => c.GetProperty("distance").GetString()!, c => c.GetProperty("start").GetInt64());
            Assert.Equal(new DateTimeOffset(gun).ToUnixTimeMilliseconds(), starts["5K"]);
            Assert.Equal(new DateTimeOffset(gun.AddSeconds(600.5)).ToUnixTimeMilliseconds(), starts["10K"]);
            Assert.InRange(data.GetProperty("now").GetInt64(),
                DateTimeOffset.Now.AddMinutes(-1).ToUnixTimeMilliseconds(), DateTimeOffset.Now.ToUnixTimeMilliseconds());
        }

        [Fact]
        public async Task Data_IsReusedUntilResultsChange()
        {
            Participant first = Runner("1", "First");
            Participant second = Runner("2", "Second");
            Finish(first, 5);
            Assert.Equal(["1"], Bibs(await Data()));

            // Saved, but the timing worker hasn't told the server about it yet.
            Finish(second, 3);
            JsonElement cached = await Data(refresh: false);
            Assert.Equal(["1"], Bibs(cached));
            // The time is still fresh, since the page sets its clocks from it.
            Assert.InRange(cached.GetProperty("now").GetInt64(),
                DateTimeOffset.Now.AddSeconds(-1).ToUnixTimeMilliseconds(), DateTimeOffset.Now.ToUnixTimeMilliseconds());

            Assert.Equal(["2", "1"], Bibs(await Data()));
        }

        [Fact]
        public async Task Data_IsBuiltAgainWhenTheAnnouncerReadsChange()
        {
            Participant first = Runner("1", "First");
            Assert.Empty(Bibs(await Data()));

            Seen("1", first, 5);
            Assert.Empty(Bibs(await Data(refresh: false)));

            // The worker counts each change to its list.
            FieldInfo version = typeof(AnnouncerWorker).GetField("version", BindingFlags.NonPublic | BindingFlags.Static)!;
            version.SetValue(null, (long)version.GetValue(null)! + 1);
            Assert.Equal(["1"], Bibs(await Data(refresh: false)));
        }

        [Fact]
        public async Task Data_IsBuiltAgainOnceASecondOld()
        {
            Participant first = Runner("1", "First");
            Assert.Empty(Bibs(await Data()));
            Finish(first, 5);
            Assert.Empty(Bibs(await Data(refresh: false)));

            await Task.Delay(1100);

            Assert.Equal(["1"], Bibs(await Data(refresh: false)));
        }

        [Fact]
        public async Task Data_KeepsSeparateListsWithAndWithoutUnknownRunners()
        {
            Seen("1", Runner("1", "Known"), 10);
            Seen("77", null, 5);

            Assert.Equal(["1"], Bibs(await Data()));
            Assert.Equal(["77", "1"], Bibs(await Data(unknown: true, refresh: false)));
            Assert.Equal(["1"], Bibs(await Data(refresh: false)));
        }
    }
}
