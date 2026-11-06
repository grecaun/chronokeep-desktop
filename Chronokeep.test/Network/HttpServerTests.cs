using Chronokeep.Network;
using Chronokeep.Objects;
using Chronokeep.Test.Database;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Network
{
    /// <summary>
    /// Starts the local results web server on a free localhost port against a temp database and requests pages from it.
    /// </summary>
    public class HttpServerTests : DatabaseTestBase
    {
        private readonly HttpServer server;
        private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(5) };
        private readonly int port;
        private readonly Event theEvent;

        public HttpServerTests()
        {
            theEvent = AddEvent(makeCurrent: true);
            var d = AddDistance(theEvent.Identifier);
            AddRunner(d, "12", "Jane", "F");
            AddRunner(d, "13", "Ann", "F");
            AddRunner(d, "14", "Bob", "M");
            port = FreePort();
            server = new HttpServer(Db, port, "localhost");
        }

        public override void Dispose()
        {
            server.Stop();
            client.Dispose();
            base.Dispose();
        }

        private void AddRunner(Distance d, string bib, string first, string gender, int chipSeconds = 1500)
        {
            var p = Db.GetParticipantBib(theEvent.Identifier, bib)
                ?? AddParticipant(theEvent.Identifier, d, bib, first: first, gender: gender);
            Db.AddTimingResult(new TimeResult(theEvent.Identifier, TimingConstants.TIMERESULT_DUMMYREAD, p.EventSpecific.Identifier,
                TimingConstants.LOCATION_FINISH, TimingConstants.SEGMENT_FINISH, 1, chipSeconds + int.Parse(bib), 0, TimeResult.BibToIdentifier(bib),
                chipSeconds, 0, DateTime.Now, bib, TimingConstants.TIMERESULT_STATUS_PROCESSED, ""));
        }

        private static int FreePort()
        {
            TcpListener l = new(IPAddress.Loopback, 0);
            l.Start();
            int p = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }

        private async Task<(HttpStatusCode Status, string Body, string? Type)> Get(string path)
        {
            using var response = await client.GetAsync($"http://localhost:{port}/{path}");
            return (response.StatusCode, await response.Content.ReadAsStringAsync(), response.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task ResultsPage_ListsFinishers()
        {
            foreach (string path in new[] { "", "results.htm", "results.html" })
            {
                var (status, body, type) = await Get(path);
                Assert.Equal(HttpStatusCode.OK, status);
                Assert.Equal("text/html", type);
                Assert.Contains("Jane", body);
                Assert.Contains("Bob", body);
            }
        }

        [Fact]
        public async Task ParticipantPage_ShowsThatRunner()
        {
            var (status, body, _) = await Get("part/14");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Contains("Bob", body);
            Assert.DoesNotContain("Jane", body);
        }

        [Fact]
        public async Task PrintablePage_ShowsCertificate()
        {
            var (status, body, type) = await Get("print/12");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("text/html", type);
            Assert.Contains("Jane", body);
        }

        [Fact]
        public async Task PrintablePage_StillWorksAfterViewingParticipantPage()
        {
            await Get("part/12");
            var (_, body, _) = await Get("print/12");
            Assert.Contains("Jane", body);
        }

        [Fact]
        public async Task PrintablePage_DoesNotReplaceParticipantPage()
        {
            var (_, printable, _) = await Get("print/12");
            var (_, participant, _) = await Get("part/12");
            Assert.NotEqual(printable, participant);
        }

        [Fact]
        public async Task EmailPage_RefreshesWhenResultsChange()
        {
            var p = Db.GetParticipantBib(theEvent.Identifier, "12")!;
            var (status, before, _) = await Get("email/12");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Contains("0:25:00", before);

            AddRunner(Db.GetDistance(p.EventSpecific.DistanceIdentifier)!, "12", "Jane", "F", chipSeconds: 1600);
            server.UpdateInformation();

            var (_, after, _) = await Get("email/12");
            Assert.Contains("0:26:40", after);
        }

        [Fact]
        public async Task StaticFiles_AreServedWithContentTypes()
        {
            var (cssStatus, _, cssType) = await Get("css/bootstrap.min.css");
            Assert.Equal(HttpStatusCode.OK, cssStatus);
            Assert.Equal("text/css", cssType);
            var (jsStatus, _, jsType) = await Get("js/bootstrap.bundle.min.js");
            Assert.Equal(HttpStatusCode.OK, jsStatus);
            Assert.Equal("text/javascript", jsType);
        }

        [Fact]
        public async Task UnknownPath_IsNotFound()
        {
            var (status, body, _) = await Get("nothing-here");
            Assert.Equal(HttpStatusCode.NotFound, status);
            Assert.Equal("", body);
        }

        [Fact]
        public async Task MissingStaticFile_IsNotFound()
        {
            var (status, _, _) = await Get("css/missing.css");
            Assert.Equal(HttpStatusCode.NotFound, status);
        }

        [Fact]
        public void UpdateInformation_DoesNotDoubleParticipantCounts()
        {
            server.UpdateInformation();
            server.UpdateInformation();

            var counts = (Dictionary<string, int>)typeof(HttpServer)
                .GetField("NumberOverallParticipants", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(server)!;
            Assert.Equal(3, counts["5K"]);
        }

        [Fact]
        public async Task UpdateInformation_RefreshesResults()
        {
            var (_, before, _) = await Get("");
            Assert.DoesNotContain("Zed", before);

            AddRunner(Db.GetDistances(theEvent.Identifier).Single(), "15", "Zed", "M");
            server.UpdateInformation();

            var (_, after, _) = await Get("");
            Assert.Contains("Zed", after);
        }
    }
}
