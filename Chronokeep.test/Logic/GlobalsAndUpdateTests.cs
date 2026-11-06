using Chronokeep.Helpers;
using Chronokeep.Network;
using Chronokeep.Objects;
using Chronokeep.Test.Database;
using Chronokeep.Updates;
using System.Text.Json;

namespace Chronokeep.Test.Logic
{
    public class GlobalsTests : DatabaseTestBase
    {
        private readonly (int, int, int, int) previous =
            (Globals.UploadInterval, Globals.DownloadInterval, Globals.AnnouncerWindow, Globals.SeenWindow);

        public override void Dispose()
        {
            (Globals.UploadInterval, Globals.DownloadInterval, Globals.AnnouncerWindow, Globals.SeenWindow) = previous;
            base.Dispose();
        }

        [Fact]
        public void SetupValues_ReadsIntervalsFromSettings()
        {
            Constants.Settings.SetupSettings(Db);
            Db.SetAppSetting(Constants.Settings.UPLOAD_INTERVAL, "15");
            Db.SetAppSetting(Constants.Settings.DOWNLOAD_INTERVAL, "20");
            Db.SetAppSetting(Constants.Settings.ANNOUNCER_WINDOW, "60");
            Db.SetAppSetting(Constants.Settings.SEEN_WINDOW, "3");

            Globals.SetupValues(Db);

            Assert.Equal((15, 20, 60, 3), (Globals.UploadInterval, Globals.DownloadInterval, Globals.AnnouncerWindow, Globals.SeenWindow));
        }

        [Fact]
        public void IgnoredChips_LoadFromEventMinusOne()
        {
            var e = AddEvent();
            Db.AddBibChipAssociation(-1, [new BibChipAssociation { EventId = -1, Bib = "", Chip = "IGNORE1" }]);
            Db.AddBibChipAssociation(e.Identifier, [new BibChipAssociation { EventId = e.Identifier, Bib = "1", Chip = "REAL" }]);

            Globals.UpdateIgnoredChips(Db);

            var ignored = Globals.GetIgnoredChips();
            Assert.Equal("IGNORE1", Assert.Single(ignored).Chip);
            ignored.Clear();
            Assert.Single(Globals.GetIgnoredChips());   // callers get a copy
        }
    }

    public class NetCoreTests
    {
        [Fact]
        public void TcpPort_IsInRangeAndStable()
        {
            int port = NetCore.GetTcpPort();
            Assert.InRange(port, 4488, 5588);
            Assert.Equal(port, NetCore.GetTcpPort());
        }
    }

    public class GithubReleaseTests
    {
        // A trimmed copy of one entry from GitHub's releases API.
        private const string Json = """
        [{
          "url": "https://api.github.com/repos/grecaun/chronokeep-desktop/releases/123",
          "html_url": "https://github.com/grecaun/chronokeep-desktop/releases/tag/v2.0.0",
          "id": 123456789,
          "tag_name": "v2.0.0",
          "name": "v2.0.0",
          "draft": false,
          "prerelease": false,
          "published_at": "2026-11-01T12:00:00Z",
          "author": { "login": "grecaun", "id": 42 },
          "assets": [
            { "name": "chronokeep-win-x64.zip", "size": 52428800, "browser_download_url": "https://github.com/x/win.zip" },
            { "name": "chronokeep-linux-x64.tar.gz", "size": 50000000, "browser_download_url": "https://github.com/x/linux.tar.gz" }
          ],
          "body": "Release notes"
        }]
        """;

        [Fact]
        public void Releases_Deserialize()
        {
            var release = Assert.Single(JsonSerializer.Deserialize<List<GithubRelease>>(Json)!);
            Assert.Equal("v2.0.0", release.Name);
            Assert.Equal("v2.0.0", release.TagName);
            Assert.False(release.PreRelease);
            Assert.Equal("grecaun", release.Author.Login);
            Assert.Equal(["chronokeep-win-x64.zip", "chronokeep-linux-x64.tar.gz"], release.Assets.Select(a => a.Name));
            Assert.Equal("https://github.com/x/win.zip", release.Assets[0].BrowserDownloadUrl);
        }
    }
}
