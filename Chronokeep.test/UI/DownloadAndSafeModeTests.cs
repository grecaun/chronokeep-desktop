using Chronokeep.Database;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.Test.Network;
using Chronokeep.Test.Timing;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.Timing;
using Chronokeep.UI;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.Util;
using Chronokeep.Updates;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// The update download window, against a local stand-in for the GitHub release: it downloads,
    /// checks the file against the release's checksums and attestations, and offers to install.
    /// </summary>
    public class DownloadWindowTests : UiTestBase
    {
        private readonly FakeApiServer server = new();
        private readonly string folder = Directory.CreateTempSubdirectory("ck-update-").FullName;
        private const string Installer = "fake installer bytes";

        public DownloadWindowTests() => AllowDialogs();

        public override void Dispose()
        {
            base.Dispose();
            server.Dispose();
            ProgramFolders.DataOverride = null;
            try { Directory.Delete(folder, true); } catch { }
        }

        private static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

        /// <summary>The kind of package this computer would download, and its file name in the release.</summary>
        private static string PackageName() => UpdatePackage.CurrentKind(File.Exists) switch
        {
            UpdateKind.MacDiskImage => "Chronokeep-2.1.0.dmg",
            UpdateKind.LinuxDeb => "chronokeep_2.1.0_amd64.deb",
            UpdateKind.LinuxRpm => "chronokeep-2.1.0.x86_64.rpm",
            _ => "Chronokeep-Setup-2.1.0.exe",
        };

        private void Release(string? checksum = null, bool attested = true, HttpStatusCode download = HttpStatusCode.OK)
        {
            string name = PackageName();
            server.Responder = request => request.Path switch
            {
                _ when request.Path == "files/" + name => (download, Installer),
                "files/SHA256SUMS" => (HttpStatusCode.OK, $"{checksum ?? Sha256(Installer)}  {name}\n"),
                _ when request.Path.StartsWith("attestations/") => (HttpStatusCode.OK, attested ? """{"attestations":[{"bundle":{}}]}""" : """{"attestations":[]}"""),
                _ => (HttpStatusCode.NotFound, "{}"),
            };
        }

        private DownloadWindow Open()
        {
            GithubRelease release = new()
            {
                Assets =
                [
                    new Assets { Name = PackageName(), BrowserDownloadUrl = server.Url + "files/" + PackageName() },
                    new Assets { Name = "SHA256SUMS", BrowserDownloadUrl = server.Url + "files/SHA256SUMS" },
                ],
            };
            return Show(new DownloadWindow(release, new global::Chronokeep.Updates.Version { Major = 2, Minor = 1, Patch = 0 }, MainWindow, folder, server.Url + "attestations/sha256:"));
        }

        private string Downloaded() => Directory.GetFiles(folder).SingleOrDefault() ?? "";

        [AvaloniaFact]
        public async Task A_checked_download_is_ready_to_install()
        {
            Release();
            DownloadWindow window = Open();
            Button install = Find<Button>(window, "InstallButton");

            Click(install);
            await WaitUntil(() => Find<TextBlock>(window, "DownloadLabel").Text == "Ready to install v2.1.0", because: "the download and check");

            Assert.Equal(Installer, File.ReadAllText(Downloaded()));
            Assert.Equal("Install", ContentText(install));
            Assert.True(install.IsEnabled);
            Assert.True(Find<Button>(window, "BackupDatabaseButton").IsEnabled);
            Assert.Contains(ServerRequests.Of(server), r => r.Path == "attestations/sha256:" + Sha256(Installer));
        }

        [AvaloniaFact]
        public async Task A_download_that_doesnt_match_its_checksum_is_deleted()
        {
            Release(checksum: new string('a', 64));
            DownloadWindow window = Open();

            Click(Find<Button>(window, "InstallButton"));

            Answer(await WaitForDialog("doesn't match this release's checksum"), false);
            Assert.Equal("", Downloaded());
            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public async Task A_download_github_didnt_build_is_deleted()
        {
            Release(attested: false);
            DownloadWindow window = Open();

            Click(Find<Button>(window, "InstallButton"));

            Answer(await WaitForDialog("GitHub has no record of this file"), false);
            Assert.Equal("", Downloaded());
            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public async Task A_failed_download_says_so_and_leaves_nothing_behind()
        {
            Release(download: HttpStatusCode.NotFound);
            DownloadWindow window = Open();

            Click(Find<Button>(window, "InstallButton"));

            Answer(await WaitForDialog("Unable to download update"), false);
            Assert.Equal("", Downloaded());
            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public void Cancel_closes_without_downloading()
        {
            Release();
            DownloadWindow window = Open();

            Click(Find<Button>(window, "CancelButton"));

            Assert.False(window.IsVisible);
            Assert.Empty(ServerRequests.Of(server));
        }

        [AvaloniaFact]
        public async Task Backing_up_twice_in_a_day_keeps_both_backups()
        {
            string data = Path.Combine(folder, "data");
            Directory.CreateDirectory(data);
            ProgramFolders.DataOverride = data;
            File.WriteAllText(Path.Combine(data, global::Chronokeep.UI.MainWindow.DATABASE_FILE_NAME), "database");
            Release();
            DownloadWindow window = Open();
            Button backup = Find<Button>(window, "BackupDatabaseButton");

            Click(backup);
            await Task.Delay(1100);
            Click(backup);

            string[] backups = Directory.GetFiles(data, "*-backup-" + global::Chronokeep.UI.MainWindow.DATABASE_FILE_NAME);
            Assert.Equal(2, backups.Length);
            Assert.All(backups, b => Assert.Equal("database", File.ReadAllText(b)));
            Assert.DoesNotContain("Error backing up", Find<TextBlock>(window, "BackupBlock").Text);
        }
    }

    /// <summary>Safe mode's window, run against a database in a temporary data folder.</summary>
    public class SafeModeWindowTests : UiTestBase
    {
        private readonly string data = Directory.CreateTempSubdirectory("ck-safe-").FullName;
        private MinWindow? window;

        public SafeModeWindowTests() => ProgramFolders.DataOverride = data;

        public override void Dispose()
        {
            if (window != null)
            {
                window.StopTimingController();
                for (int i = 0; i < 100 && TimingController.IsRunning(); i++) Thread.Sleep(50);
                window.Close();
                Pump();
            }
            base.Dispose();
            ProgramFolders.DataOverride = null;
            // Safe mode opens the shared memory store over its own database; forget it for the next test.
            typeof(global::Chronokeep.MemStore.MemStore).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
            if (global::Chronokeep.UI.MainWindow.MWindow == window) global::Chronokeep.UI.MainWindow.MWindow = null;
            try { Directory.Delete(data, true); } catch { }
        }

        private MinWindow Open()
        {
            window = new MinWindow();
            window.Show();
            Pump();
            return window;
        }

        [AvaloniaFact]
        public void Opens_a_database_in_the_data_folder_with_the_safe_mode_timing_page()
        {
            MinWindow safe = Open();

            Assert.True(File.Exists(Path.Combine(data, global::Chronokeep.UI.MainWindow.DATABASE_FILE_NAME)));
            Assert.Equal("", Find<TextBlock>(safe, "EventDate").Text ?? "");
            Assert.IsType<MinTimingPage>(Find<UserControl>(safe, "TheFrame").Content);
        }

        [AvaloniaFact]
        public void Shows_the_event_date_written_out()
        {
            SqLiteInterface db = new(Path.Combine(data, global::Chronokeep.UI.MainWindow.DATABASE_FILE_NAME));
            db.Initialize();
            int id = db.AddEvent(new Event("Spring Fling", new DateTime(2026, 6, 15).Ticks, "2026"));
            db.SetCurrentEvent(id);

            MinWindow safe = Open();

            Assert.Equal("Spring Fling", Find<TextBlock>(safe, "EventName").Text);
            Assert.Equal("June 15, 2026", Find<TextBlock>(safe, "EventDate").Text);
        }

        [AvaloniaFact]
        public async Task Messages_open_over_the_safe_mode_window()
        {
            MinWindow safe = Open();

            DialogBox.AsyncShow("A reader disconnected.");
            await WaitUntil(() => safe.OwnedWindows.OfType<DialogBox>().Any(), because: "the message to open");

            DialogBox message = safe.OwnedWindows.OfType<DialogBox>().Single();
            Assert.Equal("A reader disconnected.", DialogText(message));
            message.Close();
        }

        [AvaloniaFact]
        public async Task Closing_while_timing_asks_and_yes_closes()
        {
            using FakeUltra reader = new();
            MinWindow safe = Open();
            TimingSystem system = new(0, "127.0.0.1", reader.Port, Constants.Timing.LOCATION_FINISH, Constants.Readers.SYSTEM_RFID);
            safe.ConnectTimingSystem(system);
            await WaitUntil(TimingController.IsRunning, because: "timing to start");

            safe.Close();
            Pump();
            Assert.True(safe.IsVisible, "Safe mode closed without asking while timing was running.");
            DialogBox question = await WaitForOwnedDialog(safe, "Are you sure you wish to exit");
            Answer(question, true);
            Pump();

            Assert.False(safe.IsVisible);
            window = null;
        }

        [AvaloniaFact]
        public async Task Closing_while_timing_and_answering_no_stays_open()
        {
            using FakeUltra reader = new();
            MinWindow safe = Open();
            TimingSystem system = new(0, "127.0.0.1", reader.Port, Constants.Timing.LOCATION_FINISH, Constants.Readers.SYSTEM_RFID);
            safe.ConnectTimingSystem(system);
            await WaitUntil(TimingController.IsRunning, because: "timing to start");

            safe.Close();
            Answer(await WaitForOwnedDialog(safe, "Are you sure you wish to exit"), false);
            Pump();

            Assert.True(safe.IsVisible);
        }

        private static async Task<DialogBox> WaitForOwnedDialog(Window owner, string containing)
        {
            await WaitUntil(() => owner.OwnedWindows.OfType<DialogBox>().Any(d => d.IsVisible && DialogText(d).Contains(containing)),
                because: $"a message saying '{containing}'");
            return owner.OwnedWindows.OfType<DialogBox>().Last(d => d.IsVisible && DialogText(d).Contains(containing));
        }
    }
}
