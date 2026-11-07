using Chronokeep.Network;
using Chronokeep.Network.Registration;
using Chronokeep.Objects;
using Chronokeep.Test.Database;
using Chronokeep.Test.Timing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Chronokeep.Test.Network
{
    /// <summary>
    /// The registration server, with the Chronokeep Registration app played by a TCP client.
    /// Everything listens on 127.0.0.1 only.
    /// </summary>
    public class RegistrationServerTests : DatabaseTestBase
    {
        private readonly Event theEvent;
        private readonly Distance fiveK;
        private readonly FakeMainWindow fakeWindow;
        private readonly RegistrationWorker worker;
        private readonly Thread thread;
        private readonly int port;
        private readonly List<TcpClient> clients = [];

        public RegistrationServerTests()
        {
            theEvent = AddEvent(makeCurrent: true);
            fiveK = AddDistance(theEvent.Identifier, "5K");
            AddDistance(theEvent.Identifier, "10K");
            Constants.Settings.SetupSettings(Db);
            Db.SetAppSetting(Constants.Settings.SERVER_NAME, "Finish Laptop");

            TcpListener free = new(IPAddress.Loopback, 0);
            free.Start();
            port = ((IPEndPoint)free.LocalEndpoint).Port;
            free.Stop();

            worker = new RegistrationWorker(Db, FakeMainWindow.Create(out fakeWindow), IPAddress.Loopback, port);
            thread = new Thread(worker.Run) { IsBackground = true };
            thread.Start();
            Assert.True(WaitFor(worker.IsRunning));
        }

        public override void Dispose()
        {
            worker.Stop();
            thread.Join(8000);
            foreach (TcpClient c in clients) c.Dispose();
            base.Dispose();
        }

        private static bool WaitFor(Func<bool> condition, int ms = 5000)
        {
            DateTime until = DateTime.Now.AddMilliseconds(ms);
            while (DateTime.Now < until)
            {
                if (condition()) return true;
                Thread.Sleep(25);
            }
            return condition();
        }

        private sealed class App(TcpClient client)
        {
            private readonly StreamReader reader = new(client.GetStream(), Encoding.UTF8);

            public void Send(string json)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");
                client.GetStream().Write(bytes);
            }

            public void SendRaw(string text)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                client.GetStream().Write(bytes);
            }

            /// <summary>Reads messages until one with the given command arrives.</summary>
            public JsonElement Read(string command, int timeoutMs = 5000)
            {
                client.ReceiveTimeout = timeoutMs;
                while (true)
                {
                    string? line = reader.ReadLine();
                    Assert.NotNull(line);
                    JsonElement json = JsonDocument.Parse(line).RootElement;
                    if (command == json.GetProperty("command").GetString()) return json;
                }
            }

            /// <summary>True if nothing arrives in the given time.</summary>
            public bool Quiet(int ms = 1000)
            {
                client.ReceiveTimeout = ms;
                try
                {
                    return null == reader.ReadLine();
                }
                catch (IOException)
                {
                    return true;
                }
                finally
                {
                    client.ReceiveTimeout = 5000;
                }
            }
        }

        private App Connect()
        {
            TcpClient client = new();
            client.Connect(IPAddress.Loopback, port);
            client.ReceiveTimeout = 5000;
            clients.Add(client);
            return new App(client);
        }

        private static string Part(string id, string bib, string first, string last = "Runner", string distance = "5K", string gender = "F", string birthdate = "01/02/1990", string mobile = "", bool sms = false)
            => JsonSerializer.Serialize(new
            {
                id,
                bib,
                first,
                last,
                birthdate,
                gender,
                distance,
                mobile,
                sms,
                apparel = "",
                updatedAt = 0,
                chronokeepInfo = "",
                uploaded = false,
            });

        private int CallCount(string name)
        {
            lock (fakeWindow.Calls)
            {
                return fakeWindow.Calls.Count(c => c == name);
            }
        }

        [Fact]
        public void Connect_RepliesWithTheServerName()
        {
            App app = Connect();
            app.Send("{\"command\":\"connect\",\"reads\":false,\"sightings\":false}");
            JsonElement json = app.Read("registration_connection_successful");
            Assert.Equal("Finish Laptop", json.GetProperty("name").GetString());
            Assert.Equal(Constants.Network.CHRONOKEEP_REGISTRATION_TYPE, json.GetProperty("kind").GetString());
            Assert.Equal(Constants.Network.CHRONOKEEP_REGISTRATION_VERS, json.GetProperty("version").GetInt32());
        }

        [Fact]
        public void GetParticipants_SendsParticipantsAndDistances()
        {
            Participant p = AddParticipant(theEvent.Identifier, fiveK, "7", "Jane", "Runner");
            App app = Connect();

            app.Send("{\"command\":\"participant_get\"}");
            JsonElement json = app.Read("registration_participants");

            Assert.Equal(["10K", "5K"], json.GetProperty("distances").EnumerateArray().Select(d => d.GetString()).Order());
            JsonElement sent = Assert.Single(json.GetProperty("participants").EnumerateArray());
            Assert.Equal(p.EventSpecific.Identifier.ToString(), sent.GetProperty("id").GetString());
            Assert.Equal("7", sent.GetProperty("bib").GetString());
            Assert.Equal("Jane", sent.GetProperty("first").GetString());
            Assert.Equal("5K", sent.GetProperty("distance").GetString());
        }

        [Fact]
        public void AddParticipant_SavesItAndSendsEveryAppTheNewList()
        {
            App adder = Connect();
            App other = Connect();
            adder.Send("{\"command\":\"participant_get\"}");
            adder.Read("registration_participants");
            other.Send("{\"command\":\"participant_get\"}");
            other.Read("registration_participants");

            adder.Send($"{{\"command\":\"participant_add\",\"participant\":{Part("abc-123", "9", "Sam", mobile: "5555550100", sms: true)}}}");

            JsonElement json = other.Read("registration_participants");
            Assert.Contains(json.GetProperty("participants").EnumerateArray(), p => "Sam" == p.GetProperty("first").GetString());
            Participant saved = Assert.Single(Db.GetParticipants(theEvent.Identifier));
            Assert.Equal("Sam", saved.FirstName);
            Assert.Equal("9", saved.Bib);
            Assert.Equal("5K", saved.Distance);
            Assert.True(saved.EventSpecific.SmsEnabled);
            Assert.True(WaitFor(() => CallCount("UpdateParticipantsFromRegistration") >= 1));
        }

        [Fact]
        public void AddParticipant_UnknownDistance_IsAnError()
        {
            App app = Connect();
            app.Send($"{{\"command\":\"participant_add\",\"participant\":{Part("abc-123", "9", "Sam", distance: "Marathon")}}}");
            Assert.Equal("DISTANCE_NOT_FOUND", app.Read("registration_error").GetProperty("error").GetString());
            Assert.Empty(Db.GetParticipants(theEvent.Identifier));
        }

        [Fact]
        public void UpdateParticipant_ChangesTheSavedParticipant()
        {
            Participant p = AddParticipant(theEvent.Identifier, fiveK, "7", "Jane", "Runner");
            App app = Connect();

            app.Send($"{{\"command\":\"participant_update\",\"participant\":{Part(p.EventSpecific.Identifier.ToString(), "70", "Jane", distance: "10K")}}}");

            app.Read("registration_participants");
            Participant updated = Assert.Single(Db.GetParticipants(theEvent.Identifier));
            Assert.Equal("70", updated.Bib);
            Assert.Equal("10K", updated.Distance);
        }

        [Fact]
        public void UpdateParticipant_Unknown_IsAnError()
        {
            App app = Connect();
            app.Send($"{{\"command\":\"participant_update\",\"participant\":{Part("999", "70", "Nobody")}}}");
            Assert.Equal("PARTICIPANT_NOT_FOUND", app.Read("registration_error").GetProperty("error").GetString());
        }

        [Fact]
        public void AddUpdateParticipants_AddsNewAndUpdatesExisting()
        {
            Participant p = AddParticipant(theEvent.Identifier, fiveK, "7", "Jane", "Runner");
            App app = Connect();

            app.Send("{\"command\":\"participant_add_update\",\"participants\":[" +
                Part(p.EventSpecific.Identifier.ToString(), "77", "Jane") + "," +
                Part("", "8", "Max", gender: "M") + "," +
                Part("", "9", "Lost", distance: "Marathon") + "]}");

            Assert.True(WaitFor(() => Db.GetParticipants(theEvent.Identifier).Count == 2));
            List<Participant> saved = Db.GetParticipants(theEvent.Identifier);
            Assert.Equal("77", saved.Single(x => "Jane" == x.FirstName).Bib);
            Assert.Equal("8", saved.Single(x => "Max" == x.FirstName).Bib);
        }

        [Fact]
        public void AddUpdateParticipants_SendsEveryAppTheNewList()
        {
            App adder = Connect();
            App other = Connect();
            other.Send("{\"command\":\"participant_get\"}");
            other.Read("registration_participants");

            adder.Send("{\"command\":\"participant_add_update\",\"participants\":[" + Part("", "8", "Max", gender: "M") + "]}");

            JsonElement json = other.Read("registration_participants");
            Assert.Contains(json.GetProperty("participants").EnumerateArray(), p => "Max" == p.GetProperty("first").GetString());
        }

        [Fact]
        public void UnknownCommand_IsAnError()
        {
            App app = Connect();
            app.Send("{\"command\":\"make_coffee\"}");
            Assert.Equal("UNKNOWN_MESSAGE", app.Read("registration_error").GetProperty("error").GetString());
        }

        [Fact]
        public void MessageSplitAcrossPackets_IsHandledOnceComplete()
        {
            App app = Connect();
            app.SendRaw("{\"command\":\"conn");
            Assert.True(app.Quiet(300));
            app.SendRaw("ect\"}\n");
            Assert.Equal("Finish Laptop", app.Read("registration_connection_successful").GetProperty("name").GetString());
        }

        [Fact]
        public void Stop_TellsTheAppsAndStops()
        {
            App app = Connect();
            app.Send("{\"command\":\"connect\"}");
            app.Read("registration_connection_successful");

            worker.Stop();

            // The server checks whether to stop every half second.
            app.Read("disconnect", 2000);
            Assert.True(thread.Join(8000));
            Assert.False(worker.IsRunning());
        }
    }

    /// <summary>Discovery replies, listening on 127.0.0.1 instead of the multicast group.</summary>
    public class DiscoveryTests : IDisposable
    {
        private readonly int port;
        private readonly string directory = Path.Combine(Path.GetTempPath(), "chronokeep-tests", Guid.NewGuid().ToString("N"));
        private ZeroConf? server;
        private Thread? thread;

        public DiscoveryTests()
        {
            // Errors go here instead of whatever log the last test used.
            Directory.CreateDirectory(directory);
            Helpers.Globals.ErrorLogPath = Path.Combine(directory, "error_log.txt");
            using UdpClient free = new(new IPEndPoint(IPAddress.Loopback, 0));
            port = ((IPEndPoint)free.Client.LocalEndPoint!).Port;
        }

        public void Dispose()
        {
            server?.Stop();
            thread?.Join(5000);
            try { Directory.Delete(directory, true); } catch { }
        }

        private void Start(string? name)
        {
            server = new ZeroConf(name, IPAddress.Loopback, port);
            thread = new Thread(server.Run) { IsBackground = true };
            thread.Start();
            Thread.Sleep(200);
        }

        private static string? Ask(int port, string message)
        {
            using UdpClient client = new(new IPEndPoint(IPAddress.Loopback, 0));
            client.Client.ReceiveTimeout = 2000;
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            client.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, port));
            IPEndPoint from = new(IPAddress.Any, 0);
            try
            {
                return Encoding.UTF8.GetString(client.Receive(ref from));
            }
            catch (SocketException)
            {
                return null;
            }
        }

        [Fact]
        public void Discover_RepliesWithNameIdAndRegistrationPort()
        {
            Start("Finish Laptop");

            string? reply = Ask(port, Constants.Network.CHRONOKEEP_ZCONF_CONNECT_MSG);

            // The Registration app strips the brackets and splits on '|' (ServerFinder.kt).
            Assert.NotNull(reply);
            Assert.StartsWith("[", reply);
            Assert.EndsWith("]", reply);
            string[] parts = reply.Trim('[', ']').Split('|');
            Assert.Equal(3, parts.Length);
            Assert.Equal("Finish Laptop", parts[0]);
            Assert.Equal(10, parts[1].Length);
            Assert.Equal(NetCore.GetTcpPort().ToString(), parts[2]);
        }

        [Fact]
        public void Discover_WithoutAName_UsesTheDefault()
        {
            Start(null);
            string? reply = Ask(port, Constants.Network.CHRONOKEEP_ZCONF_CONNECT_MSG);
            Assert.StartsWith($"[{Constants.Network.DEFAULT_CHRONOKEEP_SERVER_NAME}|", reply);
        }

        [Fact]
        public void Discover_IgnoresCaseAndSurroundingSpace()
        {
            Start("Finish Laptop");
            Assert.NotNull(Ask(port, $"  {Constants.Network.CHRONOKEEP_ZCONF_CONNECT_MSG.ToLower()}\n"));
        }

        [Fact]
        public void OtherMessages_GetNoReply()
        {
            Start("Finish Laptop");
            Assert.Null(Ask(port, "hello"));
            // Still answers afterwards.
            Assert.NotNull(Ask(port, Constants.Network.CHRONOKEEP_ZCONF_CONNECT_MSG));
        }

        [Fact]
        public void Stop_EndsTheServer()
        {
            Start("Finish Laptop");
            Assert.True(ZeroConf.IsRunning());
            server!.Stop();
            Assert.True(thread!.Join(5000));
            Assert.False(ZeroConf.IsRunning());
            // A normal stop isn't an error.
            string log = Path.Combine(directory, "error_log.txt");
            Assert.False(File.Exists(log) && File.ReadAllText(log).Contains("ZeroConf"));
        }
    }
}
