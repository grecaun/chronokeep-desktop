using Chronokeep.Objects;

namespace Chronokeep.Test.MemStore
{
    public class MemStoreInitializeTests : MemStoreTestBase
    {
        [Fact]
        public void Initialize_LoadsGlobalDataFromDatabase()
        {
            Db.SetAppSetting(Constants.Settings.COMPANY_NAME, "Acme Timing");
            int apiId = Db.AddApi(new ApiObject(0, Constants.ApiConstants.CHRONOKEEP_RESULTS, "https://a", "Nick", "tok", ""));
            Db.AddTimingSystem(new TimingSystem(0, "10.0.0.5", 4488, Constants.Timing.LOCATION_FINISH, Constants.Readers.SYSTEM_CHRONOKEEP_PORTAL));
            Db.AddBannedPhone("555");
            Db.AddBannedEmail("x@y.com");
            var e = AddEvent();

            Reload();

            Assert.Equal("Acme Timing", Store.GetAppSetting(Constants.Settings.COMPANY_NAME)!.Value);
            Assert.NotNull(Store.GetApi(apiId));
            Assert.Single(Store.GetTimingSystems());
            Assert.Equal(["555"], Store.GetBannedPhones());
            Assert.Equal(["x@y.com"], Store.GetBannedEmails());
            Assert.Equal(e.Identifier, Assert.Single(Store.GetEvents()).Identifier);
            Assert.Null(Store.GetCurrentEvent());
        }

        [Fact]
        public void Initialize_LoadsCurrentEventData()
        {
            var e = AddEvent(makeCurrent: true);
            var d = AddDistance(e.Identifier);
            AddParticipant(e.Identifier, d, "1");
            Db.AddTimingLocation(new TimingLocation(e.Identifier, "Mile 1"));
            Db.AddChipRead(NewRead(e.Identifier, "100"));

            Reload();

            Assert.Equal(e.Identifier, Store.GetCurrentEvent()!.Identifier);
            Assert.Single(Store.GetDistances(e.Identifier));
            Assert.Single(Store.GetParticipants(e.Identifier));
            Assert.Equal("Mile 1", Assert.Single(Store.GetTimingLocations(e.Identifier)).Name);
            Assert.Single(Store.GetChipReads(e.Identifier));
        }

        [Fact]
        public void ResetDatabase_ClearsCacheAndDatabase()
        {
            var e = AddCurrentEvent();
            AddStoreDistance(e.Identifier);
            Store.AddBannedPhone("555");

            Store.ResetDatabase();

            Assert.Null(Store.GetCurrentEvent());
            Assert.Empty(Store.GetBannedPhones());
            Assert.Empty(Store.GetDistances(e.Identifier));
            Assert.Empty(Db.GetEvents());
        }
    }

    public class MemStoreSettingsTests : MemStoreTestBase
    {
        [Fact]
        public void SetAppSetting_UpdatesCacheAndDatabase()
        {
            Store.SetAppSetting("color", "blue");
            Assert.Equal("blue", Store.GetAppSetting("color")!.Value);
            Assert.Equal("blue", Db.GetAppSetting("color")!.Value);

            Store.SetAppSetting(new AppSetting { Name = "color", Value = "red" });
            Assert.Equal("red", Store.GetAppSetting("color")!.Value);
            Assert.Equal("red", Db.GetAppSetting("color")!.Value);
        }

        [Fact]
        public void GetAppSetting_Unknown_ReturnsNull()
        {
            Assert.Null(Store.GetAppSetting("not-a-setting"));
        }
    }

    public class MemStoreApiTests : MemStoreTestBase
    {
        private static ApiObject NewApi(string url = "https://api.example.com")
            => new(0, Constants.ApiConstants.CHRONOKEEP_RESULTS, url, "Nick", "tok", "");

        [Fact]
        public void AddApi_AssignsIdAndCaches()
        {
            int id = Store.AddApi(NewApi());
            Assert.True(id > 0);
            Assert.Equal("Nick", Store.GetApi(id)!.Nickname);
            Assert.NotNull(Db.GetApi(id));
        }

        [Fact]
        public void UpdateApi_UpdatesCachedObject()
        {
            int id = Store.AddApi(NewApi());
            Store.UpdateApi(new ApiObject(id, Constants.ApiConstants.CHRONOKEEP_RESULTS, "https://new", "Renamed", "tok2", "https://web"));

            var cached = Store.GetApi(id)!;
            Assert.Equal("Renamed", cached.Nickname);
            Assert.Equal("https://new", cached.Url);
            Assert.Equal("Renamed", Db.GetApi(id)!.Nickname);
        }

        [Fact]
        public void RemoveApi_RemovesFromCacheAndDatabase()
        {
            int id = Store.AddApi(NewApi());
            Store.RemoveApi(id);
            Assert.Null(Store.GetApi(id));
            Assert.Empty(Store.GetAllApi());
            Assert.Null(Db.GetApi(id));
        }
    }

    public class MemStoreTimingSystemTests : MemStoreTestBase
    {
        private const string Type = Constants.Readers.SYSTEM_CHRONOKEEP_PORTAL;

        [Fact]
        public void AddTimingSystem_CachesWithId()
        {
            var sys = new TimingSystem(0, " 10.0.0.5 ", 4488, Constants.Timing.LOCATION_FINISH, Type);
            int id = Store.AddTimingSystem(sys);
            Assert.Equal(id, Assert.Single(Store.GetTimingSystems()).SystemIdentifier);
            Assert.Single(Db.GetTimingSystems());
        }

        [Fact]
        public void UpdateTimingSystem_SameIp_UpdatesCachedSystem()
        {
            int id = Store.AddTimingSystem(new TimingSystem(0, "10.0.0.5", 4488, Constants.Timing.LOCATION_FINISH, Type));
            Store.UpdateTimingSystem(new TimingSystem(id, "10.0.0.5", 5000, Constants.Timing.LOCATION_START, Type));

            var cached = Assert.Single(Store.GetTimingSystems());
            Assert.Equal(5000, cached.Port);
            Assert.Equal(Constants.Timing.LOCATION_START, cached.LocationId);
        }

        [Fact]
        public void UpdateTimingSystem_NewIp_ReplacesCachedSystem()
        {
            int id = Store.AddTimingSystem(new TimingSystem(0, "10.0.0.5", 4488, Constants.Timing.LOCATION_FINISH, Type));
            Store.UpdateTimingSystem(new TimingSystem(id, "10.0.0.6", 4488, Constants.Timing.LOCATION_FINISH, Type));

            Assert.Equal("10.0.0.6", Assert.Single(Store.GetTimingSystems()).IpAddress);
            Assert.Equal("10.0.0.6", Assert.Single(Db.GetTimingSystems()).IpAddress);
        }

        [Fact]
        public void SetTimingSystems_ReplacesCache()
        {
            Store.AddTimingSystem(new TimingSystem(0, "10.0.0.1", 1, Constants.Timing.LOCATION_FINISH, Type));
            Store.SetTimingSystems([
                new TimingSystem(0, "10.0.0.2", 2, Constants.Timing.LOCATION_FINISH, Type),
                new TimingSystem(0, "10.0.0.3", 3, Constants.Timing.LOCATION_FINISH, Type)]);
            Assert.Equal(["10.0.0.2", "10.0.0.3"], Store.GetTimingSystems().Select(s => s.IpAddress).Order());
        }

        [Fact]
        public void RemoveTimingSystem_ById_And_ByObject()
        {
            int a = Store.AddTimingSystem(new TimingSystem(0, "10.0.0.1", 1, Constants.Timing.LOCATION_FINISH, Type));
            var b = new TimingSystem(0, "10.0.0.2", 2, Constants.Timing.LOCATION_FINISH, Type);
            Store.AddTimingSystem(b);

            Store.RemoveTimingSystem(a);
            Assert.Equal("10.0.0.2", Assert.Single(Store.GetTimingSystems()).IpAddress);

            Store.RemoveTimingSystem(b);
            Assert.Empty(Store.GetTimingSystems());
            Assert.Empty(Db.GetTimingSystems());
        }
    }

    public class MemStoreBannedTests : MemStoreTestBase
    {
        [Fact]
        public void BannedPhones_AddRemoveClear()
        {
            Store.AddBannedPhone("1");
            Store.AddBannedPhones(["2", "3"]);
            Assert.Equal(["1", "2", "3"], Store.GetBannedPhones().Order());

            Store.RemoveBannedPhone("1");
            Store.RemoveBannedPhones(["2"]);
            Assert.Equal(["3"], Store.GetBannedPhones());
            Assert.Equal(["3"], Db.GetBannedPhones());

            Store.ClearBannedPhones();
            Assert.Empty(Store.GetBannedPhones());
            Assert.Empty(Db.GetBannedPhones());
        }

        [Fact]
        public void BannedEmails_AddRemoveClear()
        {
            Store.AddBannedEmail("a@x.com");
            Store.AddBannedEmails(["b@x.com", "c@x.com"]);
            Assert.Equal(3, Store.GetBannedEmails().Count);

            Store.RemoveBannedEmail("a@x.com");
            Store.RemoveBannedEmails(["b@x.com"]);
            Assert.Equal(["c@x.com"], Store.GetBannedEmails());
            Assert.Equal(["c@x.com"], Db.GetBannedEmails());

            Store.ClearBannedEmails();
            Assert.Empty(Store.GetBannedEmails());
        }

        [Fact]
        public void AddBannedPhone_Twice_IsStoredOnce()
        {
            Store.AddBannedPhone("1");
            Store.AddBannedPhone("1");
            Assert.Single(Store.GetBannedPhones());
        }
    }

    public class MemStoreClockTests : MemStoreTestBase
    {
        [Fact]
        public void Clocks_AddUpdateRemove()
        {
            // Clocks are only loaded into the cache with an event, so load one first.
            AddCurrentEvent();
            int a = Store.AddClock(new Chronoclock { Name = "A", Url = "1" });
            int b = Store.AddClock(new Chronoclock { Name = "B", Url = "2" });
            Assert.Equal(2, Store.GetClocks().Count);

            Store.UpdateClock(new Chronoclock { Identifier = a, Name = "A2", Url = "1", Enabled = true });
            Assert.Contains(Store.GetClocks(), c => c.Name == "A2" && c.Enabled);

            Store.RemoveClocks([new Chronoclock { Identifier = b }]);
            Assert.Equal("A2", Assert.Single(Store.GetClocks()).Name);
            Assert.Equal("A2", Assert.Single(Db.GetClocks()).Name);
        }

        [Fact]
        public void SetCurrentEvent_LoadsClocksFromDatabase()
        {
            Db.AddClock(new Chronoclock { Name = "Finish", Url = "10.0.0.9" });
            AddCurrentEvent();
            Assert.Equal("Finish", Assert.Single(Store.GetClocks()).Name);
        }
    }
}
