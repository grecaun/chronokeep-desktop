using Chronokeep.Objects;
using Chronokeep.Objects.Changelog;
using Chronokeep.Objects.Notifications;
using Chronokeep.Objects.Registration;
using Chronokeep.Test.Database;
using System.Text.Json;
using TimingConstants = Chronokeep.Constants.Timing;
using RegistrationParticipant = Chronokeep.Objects.Registration.Participant;

namespace Chronokeep.Test.Logic
{
    /// <summary>The changelog files shipped with the program, as the changelog window loads them.</summary>
    public class ChangelogFileTests
    {
        /// <summary>Chronokeep/changelog in the source tree, found by walking up from the test output folder.</summary>
        private static string SourceFolder()
        {
            DirectoryInfo? dir = new(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "Chronokeep", "changelog");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Couldn't find Chronokeep/changelog above the test output folder.");
        }

        private static string[] Files() => Directory.GetFiles(SourceFolder());

        private static Entry Load(string file) => JsonSerializer.Deserialize<Entry>(File.ReadAllText(file))!;

        public static TheoryData<string> FileNames()
        {
            TheoryData<string> data = [];
            foreach (string f in Files()) data.Add(Path.GetFileName(f));
            return data;
        }

        [Theory]
        [MemberData(nameof(FileNames))]
        public void File_ParsesWithItsVersion(string name)
        {
            Entry entry = Load(Path.Combine(SourceFolder(), name));
            Assert.Equal($"v{Path.GetFileNameWithoutExtension(name)}", entry.Version);
            Assert.True(entry.ChangesList.Count + entry.FixesList.Count > 0, "A changelog should list at least one change or fix.");
            Assert.All(entry.ChangesList.Concat(entry.FixesList), line => Assert.False(string.IsNullOrWhiteSpace(line)));
            Assert.Equal(entry.ChangesList.Count, entry.ChangesList.Distinct().Count());
            Assert.Equal(entry.FixesList.Count, entry.FixesList.Distinct().Count());
        }

        [Fact]
        public void EveryFileIsJson()
        {
            // The window deserializes every file in the folder, so anything else would break it.
            Assert.All(Files(), f => Assert.Equal(".json", Path.GetExtension(f)));
        }

        [Fact]
        public void TheWindowHasAtLeastFiveToShow()
        {
            // ChangeLogWindow shows the newest five with [..5].
            Assert.True(Files().Length >= 5);
        }

        [Fact]
        public void EveryFile_IsCopiedToTheBuildOutput()
        {
            string output = Path.Combine(AppContext.BaseDirectory, "changelog");
            Assert.Equal(
                Files().Select(Path.GetFileName).Order(),
                Directory.GetFiles(output).Select(Path.GetFileName).Order());
        }

        [Fact]
        public void Files_SortNewestFirst()
        {
            List<Entry> entries = [.. Files().Select(Load)];
            entries.Sort();
            Assert.Equal("v2.0.0", entries[0].Version);
            // 1.9.10 is newer than 1.9.9 even though it sorts lower as text.
            Assert.True(entries.FindIndex(e => e.Version == "v1.9.10") < entries.FindIndex(e => e.Version == "v1.9.9"));
            Assert.Equal("v1.7.0", entries[^1].Version);
        }
    }

    public class ChangelogEntryTests
    {
        private static Entry E(string version) => new() { Version = version };

        [Fact]
        public void Sort_ComparesEachNumber()
        {
            List<Entry> entries = [E("v1.9.9"), E("v2.0.0"), E("v1.10.0"), E("v1.9.10"), E("v2.0.1")];
            entries.Sort();
            Assert.Equal(["v2.0.1", "v2.0.0", "v1.10.0", "v1.9.10", "v1.9.9"], entries.Select(e => e.Version));
        }

        [Fact]
        public void Sort_UnreadableVersions_FallBackToText()
        {
            List<Entry> entries = [E("alpha"), E("beta")];
            entries.Sort();
            Assert.Equal(["beta", "alpha"], entries.Select(e => e.Version));
        }

        [Fact]
        public void CompareTo_SomethingElse_IsMinusOne()
        {
            Assert.Equal(-1, E("v1.0.0").CompareTo("v1.0.0"));
            Assert.Throws<ArgumentNullException>(() => E("v1.0.0").CompareTo(null));
        }

        [Fact]
        public void Visibility_FollowsTheLists()
        {
            Entry e = new() { ChangesList = ["Something."] };
            Assert.True(e.ChangesVisibility);
            Assert.False(e.FixesVisibility);
        }
    }

    public class MailgunCredentialsTests : DatabaseTestBase
    {
        private MailgunCredentials Load(string key, string domain, string fromEmail, string fromName)
        {
            Db.SetAppSetting(Constants.Settings.MAILGUN_API_KEY, key);
            Db.SetAppSetting(Constants.Settings.MAILGUN_API_URL, domain);
            Db.SetAppSetting(Constants.Settings.MAILGUN_FROM_EMAIL, fromEmail);
            Db.SetAppSetting(Constants.Settings.MAILGUN_FROM_NAME, fromName);
            return MailgunCredentials.GetCredentials(Db);
        }

        [Fact]
        public void FromSettings()
        {
            MailgunCredentials c = Load("key-123", "mg.example.com", "results@example.com", "Chronokeep Results");
            Assert.True(c.Valid());
            Assert.Equal("key-123", c.ApiKey);
            Assert.Equal("mg.example.com", c.Domain);
            Assert.Equal("api", c.Username);
            Assert.Equal("Chronokeep Results <results@example.com>", c.From());
        }

        [Fact]
        public void WithoutAName_FromIsJustTheEmail()
        {
            Assert.Equal("results@example.com", Load("key-123", "mg.example.com", "results@example.com", "").From());
        }

        [Theory]
        [InlineData("", "mg.example.com", "results@example.com")]
        [InlineData("key-123", "", "results@example.com")]
        [InlineData("key-123", "mg.example.com", "")]
        public void MissingAnyRequiredValue_IsNotValid(string key, string domain, string fromEmail)
        {
            Assert.False(Load(key, domain, fromEmail, "Name").Valid());
        }

        [Fact]
        public void NewDatabase_IsNotValid()
        {
            // The main window sets up every setting when the program starts.
            Constants.Settings.SetupSettings(Db);
            Assert.False(MailgunCredentials.GetCredentials(Db).Valid());
        }
    }

    /// <summary>
    /// Messages between the desktop registration server and the Chronokeep Registration app
    /// (D:\Chronokeep\registration, objects/responses and objects/registration).
    /// </summary>
    public class RegistrationMessageTests
    {
        private static JsonElement Json(object o) => JsonDocument.Parse(JsonSerializer.Serialize(o)).RootElement;

        [Fact]
        public void ConnectionSuccessful()
        {
            JsonElement json = Json(new ConnectionSuccessfulResponse { Name = "Finish Laptop", Type = "chronokeep_desktop", Version = 1 });
            Assert.Equal("registration_connection_successful", json.GetProperty("command").GetString());
            Assert.Equal("Finish Laptop", json.GetProperty("name").GetString());
            Assert.Equal("chronokeep_desktop", json.GetProperty("kind").GetString());
            Assert.Equal(1, json.GetProperty("version").GetInt32());
        }

        [Fact]
        public void Participants_HaveEveryFieldTheAppReads()
        {
            JsonElement json = Json(new ParticipantsResponse
            {
                Participants =
                [
                    new RegistrationParticipant { Id = "12", Bib = "7", FirstName = "Jane", LastName = "Runner", Birthdate = "6/16/1990", Gender = "F", Distance = "5K", Mobile = "5555550100", SmsEnabled = true, Apparel = "M" }
                ],
                Distances = ["5K", "10K"],
            });
            Assert.Equal("registration_participants", json.GetProperty("command").GetString());
            Assert.Equal(["5K", "10K"], json.GetProperty("distances").EnumerateArray().Select(d => d.GetString()));
            JsonElement p = Assert.Single(json.GetProperty("participants").EnumerateArray());
            Assert.Equal("12", p.GetProperty("id").GetString());
            Assert.Equal("7", p.GetProperty("bib").GetString());
            Assert.Equal("Jane", p.GetProperty("first").GetString());
            Assert.Equal("Runner", p.GetProperty("last").GetString());
            Assert.Equal("6/16/1990", p.GetProperty("birthdate").GetString());
            Assert.Equal("F", p.GetProperty("gender").GetString());
            Assert.Equal("5K", p.GetProperty("distance").GetString());
            Assert.Equal("5555550100", p.GetProperty("mobile").GetString());
            Assert.True(p.GetProperty("sms").GetBoolean());
            Assert.Equal("M", p.GetProperty("apparel").GetString());
        }

        [Theory]
        [InlineData(RegistrationError.UNKNOWN_MESSAGE)]
        [InlineData(RegistrationError.PARTICIPANT_NOT_FOUND)]
        [InlineData(RegistrationError.DISTANCE_NOT_FOUND)]
        public void Error_UsesTheAppsErrorNames(string error)
        {
            JsonElement json = Json(new ErrorResponse { Error = error });
            Assert.Equal("registration_error", json.GetProperty("command").GetString());
            Assert.Equal(error, json.GetProperty("error").GetString());
        }

        [Fact]
        public void Disconnect()
        {
            Assert.Equal("disconnect", Json(new DisconnectResponse()).GetProperty("command").GetString());
        }

        [Fact]
        public void RequestCommands_MatchTheApp()
        {
            Assert.Equal("participant_get", Request.GET_PARTICIPANTS);
            Assert.Equal("participant_update", Request.UPDATE_PARTICIPANT);
            Assert.Equal("participant_add", Request.ADD_PARTICIPANT);
            Assert.Equal("participant_add_update", Request.ADD_UPDATE_PARTICIPANT);
            Assert.Equal("connect", Request.CONNECT);
        }

        [Fact]
        public void ParticipantFromTheApp_IsRead_IgnoringTheAppsOwnFields()
        {
            // What the app's AddParticipantRequest sends, including its database-only fields.
            const string json = "{\"command\":\"participant_add\",\"participant\":{\"id\":\"12\",\"bib\":\"7\",\"first\":\"Jane\",\"last\":\"Runner\",\"birthdate\":\"6/16/1990\",\"gender\":\"F\",\"distance\":\"5K\",\"mobile\":\"5555550100\",\"sms\":true,\"apparel\":\"M\",\"updatedAt\":1781510400,\"chronokeepInfo\":\"\",\"uploaded\":false}}";
            ModifyParticipant request = JsonSerializer.Deserialize<ModifyParticipant>(json)!;
            Assert.Equal(Request.ADD_PARTICIPANT, request.Command);
            Assert.Equal("12", request.Participant.Id);
            Assert.Equal("Jane", request.Participant.FirstName);
            Assert.True(request.Participant.SmsEnabled);
            Assert.Equal("M", request.Participant.Apparel);
        }

        [Fact]
        public void SeveralParticipantsFromTheApp()
        {
            const string json = "{\"command\":\"participant_add_update\",\"participants\":[{\"id\":\"1\",\"first\":\"A\"},{\"id\":\"2\",\"first\":\"B\"}]}";
            ModifyMultipleParticipants request = JsonSerializer.Deserialize<ModifyMultipleParticipants>(json)!;
            Assert.Equal(["1", "2"], request.Participants.Select(p => p.Id));
        }
    }

    public class StatsParticipantTests
    {
        [Fact]
        public void ShowsTheParticipantAndWhenTheyWereLastSeen()
        {
            Objects.Participant p = new("Jane", "Runner", "", "", "", "", "",
                new EventSpecific(1, 1, "5K", "7", 0, "", "", "", false, false, "", ""), "", "", "5555550100", "", "", "", "F", "", "");
            StatsParticipant s = new(p, "Mile 3", "08:25:03");
            Assert.Equal("7", s.Bib);
            Assert.Equal("Jane", s.FirstName);
            Assert.Equal("Runner", s.LastName);
            Assert.Equal("Mile 3", s.LastSeen);
            Assert.Equal("08:25:03", s.LastSeenTime);
            Assert.Same(p, s.GetParticipant());
        }
    }
}
