using Chronokeep.Network.API;
using Chronokeep.Network.Remote;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Chronokeep.Test.Network
{
    /// <summary>
    /// A localhost HTTP server that records each request and answers with a canned status and body.
    /// </summary>
    public sealed class FakeApiServer : IDisposable
    {
        public record Request(string Method, string Path, string? Authorization, string Body);

        private readonly HttpListener listener = new();
        private readonly Task loop;
        public string Url { get; }
        public List<Request> Requests { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string ResponseBody { get; set; } = "{}";

        public FakeApiServer()
        {
            TcpListener probe = new(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            Url = $"http://localhost:{port}/";
            listener.Prefixes.Add(Url);
            listener.Start();
            loop = Task.Run(Serve);
        }

        private async Task Serve()
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await listener.GetContextAsync(); }
                catch { return; }
                using StreamReader reader = new(context.Request.InputStream, Encoding.UTF8);
                lock (Requests)
                {
                    Requests.Add(new Request(context.Request.HttpMethod, context.Request.Url!.AbsolutePath.TrimStart('/'),
                        context.Request.Headers["Authorization"], reader.ReadToEnd()));
                }
                byte[] body = Encoding.UTF8.GetBytes(ResponseBody);
                context.Response.StatusCode = (int)Status;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = body.Length;
                context.Response.OutputStream.Write(body);
                context.Response.Close();
            }
        }

        public Request Single() => Assert.Single(Requests);

        public JsonElement SingleBody() => JsonDocument.Parse(Single().Body).RootElement;

        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            listener.Stop();
            listener.Close();
            loop.Wait(TimeSpan.FromSeconds(2));
        }
    }

    public class ResultsApiClientTests : IDisposable
    {
        private readonly FakeApiServer server = new();
        private readonly ApiObject api;

        public ResultsApiClientTests()
        {
            api = new ApiObject(1, Constants.ApiConstants.CHRONOKEEP_RESULTS, server.Url, "Test", "secret-token", "");
        }

        public void Dispose()
        {
            server.Dispose();
            GC.SuppressFinalize(this);
        }

        private static ApiResult Result() => new(
            new Event { Identifier = 1, Name = "Race", Date = new DateTime(2026, 6, 15).ToShortDateString() },
            new TimeResult(1, -1, 42, Constants.Timing.LOCATION_FINISH, Constants.Timing.SEGMENT_FINISH, 1, 1500, 0, "Bib:7",
                1490, 0, DateTime.Now, "7", Constants.Timing.TIMERESULT_STATUS_NONE, ""),
            new DateTime(2026, 6, 15, 8, 0, 0), "XYZ");

        [Theory]
        [InlineData(HttpStatusCode.OK)]
        [InlineData(HttpStatusCode.NoContent)]
        public async Task IsHealthy_TrueOnSuccess(HttpStatusCode status)
        {
            server.Status = status;
            server.ResponseBody = "";
            Assert.True(await ApiHandlers.IsHealthy(api));
            Assert.Equal(("GET", "health"), (server.Single().Method, server.Single().Path));
        }

        [Fact]
        public async Task IsHealthy_ThrowsOnServerError()
        {
            server.Status = HttpStatusCode.ServiceUnavailable;
            await Assert.ThrowsAsync<ApiException>(() => ApiHandlers.IsHealthy(api));
        }

        [Fact]
        public async Task UnreachableServer_ThrowsApiException()
        {
            server.Dispose();
            await Assert.ThrowsAsync<ApiException>(() => ApiHandlers.GetEvents(api));
        }

        [Fact]
        public async Task GetEvents_SendsBearerTokenAndParsesEvents()
        {
            server.ResponseBody = "{\"events\":[{\"name\":\"Race\",\"slug\":\"race\"},{\"name\":\"Other\",\"slug\":\"other\"}]}";

            var response = await ApiHandlers.GetEvents(api);

            var request = server.Single();
            Assert.Equal(("GET", "event/my"), (request.Method, request.Path));
            Assert.Equal("Bearer secret-token", request.Authorization);
            Assert.Equal(["race", "other"], response.Events.Select(e => e.Slug));
        }

        [Fact]
        public async Task UploadResults_PostsSlugYearAndResults()
        {
            server.ResponseBody = "{\"count\":1}";

            var response = await ApiHandlers.UploadResults(api, "race", "2026", [Result()]);

            Assert.Equal(1, response.Count);
            var request = server.Single();
            Assert.Equal(("POST", "results/add"), (request.Method, request.Path));
            var body = server.SingleBody();
            Assert.Equal("race", body.GetProperty("slug").GetString());
            Assert.Equal("2026", body.GetProperty("year").GetString());
            var result = Assert.Single(body.GetProperty("results").EnumerateArray());
            Assert.Equal("42-XYZ", result.GetProperty("person_id").GetString());
            Assert.Equal(1500, result.GetProperty("seconds").GetInt32());
        }

        [Fact]
        public async Task DeleteResults_SendsDeleteWithSlugAndYear()
        {
            server.ResponseBody = "{\"count\":3}";

            var response = await ApiHandlers.DeleteResults(api, "race", "2026");

            Assert.Equal(3, response.Count);
            Assert.Equal(("DELETE", "results/delete"), (server.Single().Method, server.Single().Path));
            Assert.Equal("race", server.SingleBody().GetProperty("slug").GetString());
        }

        [Fact]
        public async Task GetParticipants_SendsPaging()
        {
            server.ResponseBody = "{\"participants\":[{\"id\":\"p1\",\"bib\":\"7\",\"first\":\"Jane\",\"distance\":\"5K\"}]}";

            var response = await ApiHandlers.GetParticipants(api, "race", "2026", 50, 2);

            var body = server.SingleBody();
            Assert.Equal(50, body.GetProperty("limit").GetInt32());
            Assert.Equal(2, body.GetProperty("page").GetInt32());
            Assert.Equal("Jane", Assert.Single(response.Participants).First);
        }

        [Fact]
        public async Task UploadBibChips_PostsBibChips()
        {
            server.ResponseBody = "{\"count\":2}";

            await ApiHandlers.UploadBibChips(api, "race", "2026", [new BibChip { Bib = "1", Chip = "A" }, new BibChip { Bib = "2", Chip = "B" }]);

            Assert.Equal("bibchips/add", server.Single().Path);
            Assert.Equal(["A", "B"], server.SingleBody().GetProperty("bib_chips").EnumerateArray().Select(b => b.GetProperty("chip").GetString()));
        }

        [Fact]
        public async Task ErrorResponse_MessageBecomesTheExceptionMessage()
        {
            server.Status = HttpStatusCode.Unauthorized;
            server.ResponseBody = "{\"message\":\"Expired Key\"}";

            var ex = await Assert.ThrowsAsync<ApiException>(() => ApiHandlers.UploadResults(api, "race", "2026", [Result()]));

            Assert.Contains("Expired Key", ex.Message);
        }

        [Fact]
        public async Task NonJsonErrorResponse_StillThrowsApiException()
        {
            server.Status = HttpStatusCode.BadGateway;
            server.ResponseBody = "<html><body>502 Bad Gateway</body></html>";

            await Assert.ThrowsAsync<ApiException>(() => ApiHandlers.GetEvents(api));
        }

        [Fact]
        public async Task AddBannedPhone_InvalidNumber_FailsWithoutCallingTheServer()
        {
            var ex = await Assert.ThrowsAsync<ApiException>(() => ApiHandlers.AddBannedPhone("12345"));
            Assert.Equal("Invalid phone number.", ex.Message);
            Assert.Empty(server.Requests);
        }
    }

    public class RemoteApiClientTests : IDisposable
    {
        private readonly FakeApiServer server = new();
        private readonly ApiObject api;

        public RemoteApiClientTests()
        {
            api = new ApiObject(1, Constants.ApiConstants.CHRONOKEEP_REMOTE, server.Url, "Remote", "remote-token", "");
        }

        public void Dispose()
        {
            server.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task GetReaders_ParsesReaders()
        {
            server.ResponseBody = "{\"readers\":[{\"name\":\"Finish\"},{\"name\":\"Start\"}]}";

            var response = await RemoteHandlers.GetReaders(api);

            Assert.Equal(("GET", "readers"), (server.Single().Method, server.Single().Path));
            Assert.Equal("Bearer remote-token", server.Single().Authorization);
            Assert.Equal(["Finish", "Start"], response.Readers.Select(r => r.Name));
        }

        [Fact]
        public async Task GetReads_SendsReaderAndTimeRange()
        {
            server.ResponseBody = "{\"count\":1,\"reads\":[{\"identifier\":\"CHIP1\",\"seconds\":100,\"milliseconds\":5,\"ident_type\":\"chip\",\"type\":\"reader\"}]}";

            var response = await RemoteHandlers.GetReads(api, "Finish", 1000, 2000);

            var body = server.SingleBody();
            Assert.Equal(("GET", "reads"), (server.Single().Method, server.Single().Path));
            Assert.Equal("Finish", body.GetProperty("reader").GetString());
            Assert.Equal(1000, body.GetProperty("start").GetInt64());
            Assert.Equal(2000, body.GetProperty("end").GetInt64());
            Assert.Equal("CHIP1", Assert.Single(response.Reads!).Identifier);
        }

        [Fact]
        public async Task DeleteReads_ReturnsCount()
        {
            server.ResponseBody = "{\"count\":4}";

            var response = await RemoteHandlers.DeleteReads(api, "Finish", 1000, 2000);

            Assert.Equal(("DELETE", "reads/delete"), (server.Single().Method, server.Single().Path));
            Assert.Equal(4, response.Count);
        }

        [Fact]
        public async Task ApiObject_GetReaders_RequiresRemoteType()
        {
            var results = new ApiObject(1, Constants.ApiConstants.CHRONOKEEP_RESULTS, server.Url, "Results", "t", "");
            await Assert.ThrowsAnyAsync<Exception>(() => results.GetReaders());
            Assert.Empty(server.Requests);
        }

        [Fact]
        public async Task ApiObject_GetReads_ConvertsToChipReads()
        {
            server.ResponseBody = "{\"count\":1,\"reads\":[{\"identifier\":\"CHIP1\",\"seconds\":1781539200,\"milliseconds\":5,\"ident_type\":\"chip\",\"type\":\"reader\"}]}";
            var reader = new Objects.ChronokeepRemote.RemoteReader("Finish", 1, Constants.Timing.LOCATION_FINISH, 9);

            var (reads, _) = await api.GetReads(reader, new DateTime(2026, 6, 15, 8, 0, 0), new DateTime(2026, 6, 15, 9, 0, 0));

            var read = Assert.Single(reads);
            Assert.Equal(9, read.EventId);
            Assert.Equal(Constants.Timing.LOCATION_FINISH, read.LocationId);
            Assert.Equal("CHIP1", read.ChipNumber);
        }
    }
}
