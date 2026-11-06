using Chronokeep.Objects;

namespace Chronokeep.Test.Database
{
    public class SettingsTests : DatabaseTestBase
    {
        [Fact]
        public void GetAppSetting_Missing_ReturnsNull()
        {
            Assert.Null(Db.GetAppSetting("does-not-exist"));
        }

        [Fact]
        public void SetAppSetting_ThenGet_RoundTrips()
        {
            Db.SetAppSetting("color", "blue");
            var setting = Db.GetAppSetting("color");
            Assert.NotNull(setting);
            Assert.Equal("color", setting.Name);
            Assert.Equal("blue", setting.Value);
        }

        [Fact]
        public void SetAppSetting_SameName_ReplacesValue()
        {
            Db.SetAppSetting("color", "blue");
            Db.SetAppSetting(new AppSetting { Name = "color", Value = "red" });
            Assert.Equal("red", Db.GetAppSetting("color")!.Value);
            Assert.Equal(1L, Count("settings", "setting='color'"));
        }

        [Fact]
        public void SetAppSetting_ValueWithQuotes_IsStoredSafely()
        {
            Db.SetAppSetting("quote", "it's \"quoted\"; DROP TABLE events;");
            Assert.Equal("it's \"quoted\"; DROP TABLE events;", Db.GetAppSetting("quote")!.Value);
            Assert.Equal(1L, Count("sqlite_master", "type='table' AND name='events'"));
        }
    }

    public class ApiTests : DatabaseTestBase
    {
        private static ApiObject NewApi(string url = "https://api.example.com", string token = "tok")
            => new(0, Constants.ApiConstants.CHRONOKEEP_RESULTS, url, "Nick", token, "https://web.example.com");

        [Fact]
        public void AddApi_ThenGetApi_RoundTripsAllFields()
        {
            int id = Db.AddApi(NewApi());
            var api = Db.GetApi(id);
            Assert.NotNull(api);
            Assert.Equal(id, api.Identifier);
            Assert.Equal(Constants.ApiConstants.CHRONOKEEP_RESULTS, api.Type);
            Assert.Equal("https://api.example.com", api.Url);
            Assert.Equal("Nick", api.Nickname);
            Assert.Equal("tok", api.AuthToken);
            Assert.Equal("https://web.example.com", api.WebUrl);
        }

        [Fact]
        public void GetApi_NegativeOrMissingId_ReturnsNull()
        {
            Assert.Null(Db.GetApi(-1));
            Assert.Null(Db.GetApi(9999));
        }

        [Fact]
        public void AddApi_SameUrlAndToken_ReplacesExisting()
        {
            Db.AddApi(NewApi());
            Db.AddApi(NewApi());
            Assert.Single(Db.GetAllApi());
        }

        [Fact]
        public void UpdateApi_ChangesFields()
        {
            int id = Db.AddApi(NewApi());
            var api = Db.GetApi(id)!;
            api.Nickname = "Renamed";
            api.WebUrl = "https://other.example.com";
            Db.UpdateApi(api);

            var updated = Db.GetApi(id)!;
            Assert.Equal("Renamed", updated.Nickname);
            Assert.Equal("https://other.example.com", updated.WebUrl);
        }

        [Fact]
        public void RemoveApi_DeletesApiAndDetachesEvents()
        {
            int apiId = Db.AddApi(NewApi());
            var e = AddEvent();
            e.ApiId = apiId;
            e.ApiEventId = "remote-event";
            Db.UpdateEvent(e);

            Db.RemoveApi(apiId);

            Assert.Null(Db.GetApi(apiId));
            var stored = Db.GetEvent(e.Identifier)!;
            Assert.Equal(-1, stored.ApiId);
            Assert.Equal("", stored.ApiEventId);
        }

        [Fact]
        public void GetAllApi_ReturnsEveryApi()
        {
            Db.AddApi(NewApi("https://a.example.com"));
            Db.AddApi(NewApi("https://b.example.com"));
            Assert.Equal(2, Db.GetAllApi().Count);
        }
    }
}
