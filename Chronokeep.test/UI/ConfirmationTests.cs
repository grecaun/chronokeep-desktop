using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepRemote;
using Chronokeep.Test.Network;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.Timing.Interfaces;
using Chronokeep.UI.API.Parts;
using Chronokeep.UI.API.Windows;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.MainPages.Timing;
using Chronokeep.UI.Participants;
using Chronokeep.UI.Parts;
using Chronokeep.UI.Timing.ReaderSettings;
using Chronokeep.UI.Util;
using System.Net;
using static Chronokeep.Constants.ApiConstants;
using Readers = Chronokeep.Constants.Readers;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// Pages and windows that ask before doing something that can't be undone, answered Yes and No
    /// through the real message box.
    /// </summary>
    public class PageConfirmationTests : UiTestBase
    {
        public PageConfirmationTests() => AllowDialogs();

        // ---- Dashboard: delete event ----

        private DashboardPage OpenDashboard()
        {
            DashboardPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        [AvaloniaTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Delete_event_only_deletes_when_confirmed(bool yes)
        {
            SeedEvent();
            DashboardPage page = OpenDashboard();

            Click(Find<Button>(page, "DeleteEvent"));
            Answer(await WaitForDialog("delete this event"), yes);

            Assert.Equal(yes ? 0 : 1, Db.GetEvents().Count);
            Assert.Equal(yes ? -1 : Db.GetEvents().Single().Identifier, Db.GetCurrentEvent()?.Identifier ?? -1);
            Assert.Equal(yes ? 1 : 0, MainWindow.WindowFinalizeCalls);
        }

        [AvaloniaFact]
        public async Task Delete_event_first_offers_to_stop_background_work()
        {
            SeedEvent();
            MainWindow.BackgroundRunning = true;
            DashboardPage page = OpenDashboard();

            Click(Find<Button>(page, "DeleteEvent"));
            Answer(await WaitForDialog("processes running in the background"), true);
            Answer(await WaitForDialog("delete this event"), true);

            Assert.Equal(1, MainWindow.StopBackgroundProcessesCalls);
            Assert.Empty(Db.GetEvents());
        }

        [AvaloniaFact]
        public async Task Declining_to_stop_background_work_keeps_the_event()
        {
            SeedEvent();
            MainWindow.BackgroundRunning = true;
            DashboardPage page = OpenDashboard();

            Click(Find<Button>(page, "DeleteEvent"));
            Answer(await WaitForDialog("processes running in the background"), false);
            await Settle();

            Assert.Equal(0, MainWindow.StopBackgroundProcessesCalls);
            Assert.Single(Db.GetEvents());
            Assert.Empty(OpenDialogs());
        }

        // ---- Distances: remove a distance with participants ----

        private DistancesPage OpenDistances()
        {
            DistancesPage page = new(MainWindow, Db);
            Host(page);
            page.UpdateView();
            Pump();
            return page;
        }

        private static List<string> DistanceRows(DistancesPage page) =>
            [.. Find<ListBox>(page, "DistancesBox").Items.OfType<DistancePart>().Select(r => r.GetDistance().Name)];

        private static DistancePart DistanceRow(DistancesPage page, string name) =>
            Find<ListBox>(page, "DistancesBox").Items.OfType<DistancePart>().Single(r => r.GetDistance().Name == name);

        [AvaloniaTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Removing_a_distance_with_participants_asks_first(bool yes)
        {
            Event e = AddEvent(makeCurrent: true);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            AddDistance(e.Identifier, "10K");
            AddParticipant(e.Identifier, fiveK, "1");
            DistancesPage page = OpenDistances();

            Click(Find<Button>(DistanceRow(page, "5K"), "RemoveButton"));
            Answer(await WaitForDialog("Distance has participants"), yes);

            List<string> expected = yes ? ["10K"] : ["5K", "10K"];
            Assert.Equal(expected.Order(), Db.GetDistances(e.Identifier).Select(d => d.Name).Order());
            Assert.Equal(expected.Order(), DistanceRows(page).Order());
        }

        [AvaloniaFact]
        public async Task Removing_a_distance_whose_linked_distance_has_participants_removes_both_when_confirmed()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance half = AddDistance(e.Identifier, "Half");
            AddDistance(e.Identifier, "10K");
            Distance early = AddDistance(e.Identifier, "Half Early");
            early.LinkedDistance = half.Identifier;
            Db.UpdateDistance(early);
            AddParticipant(e.Identifier, early, "1");
            DistancesPage page = OpenDistances();

            Click(Find<Button>(DistanceRow(page, "Half"), "RemoveButton"));
            Answer(await WaitForDialog("Distance has participants"), true);

            Assert.Equal(["10K"], Db.GetDistances(e.Identifier).Select(d => d.Name));
            Assert.Equal(["10K"], DistanceRows(page));
            Assert.Empty(OpenDialogs());
        }

        [AvaloniaFact]
        public async Task Declining_keeps_the_distance_and_its_linked_distances()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance half = AddDistance(e.Identifier, "Half");
            Distance early = AddDistance(e.Identifier, "Half Early");
            early.LinkedDistance = half.Identifier;
            Db.UpdateDistance(early);
            Distance late = AddDistance(e.Identifier, "Half Late");
            late.LinkedDistance = half.Identifier;
            Db.UpdateDistance(late);
            AddParticipant(e.Identifier, late, "1");
            DistancesPage page = OpenDistances();

            Click(Find<Button>(DistanceRow(page, "Half"), "RemoveButton"));
            Answer(await WaitForDialog("Distance has participants"), false);

            Assert.Equal(3, Db.GetDistances(e.Identifier).Count);
        }

        // ---- Chip assignment: clear everything ----

        private async Task<ChipAssignmentPage> OpenChips()
        {
            ChipAssignmentPage page = new(MainWindow, Db);
            Host(page);
            await WaitUntil(() => Find<DataGrid>(page, "BibChipList").ItemsSource != null, because: "chips to load");
            await Settle(100);
            return page;
        }

        [AvaloniaTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Clear_removes_every_bib_chip_pair_only_when_confirmed(bool yes)
        {
            Event e = AddEvent(makeCurrent: true);
            Db.AddBibChipAssociation(e.Identifier, [
                new BibChipAssociation { EventId = e.Identifier, Bib = "1", Chip = "1001" },
                new BibChipAssociation { EventId = e.Identifier, Bib = "2", Chip = "1002" },
            ]);
            ChipAssignmentPage page = await OpenChips();

            Click(Find<Button>(page, "Clear"));
            Answer(await WaitForDialog("delete everything"), yes);

            Assert.Equal(yes ? 0 : 2, Db.GetBibChips(e.Identifier).Count);
        }

        [AvaloniaFact]
        public async Task Clear_ignored_stops_ignoring_every_chip_when_confirmed()
        {
            AddEvent(makeCurrent: true);
            ChipAssignmentPage page = await OpenChips();
            Find<TextBox>(page, "IgnoredChipBox").Text = "9999";
            Click(Find<Button>(page, "SaveIgnored"));
            await WaitUntil(() => Find<DataGrid>(page, "IgnoredChipList").ItemsSource!.Cast<object>().Count() == 1, because: "the chip to be ignored");

            Click(Find<Button>(page, "ClearIgnored"));
            Answer(await WaitForDialog("delete everything"), true);

            Assert.Empty(Db.GetBibChips(-1));
            Assert.DoesNotContain(Globals.GetIgnoredChips(), c => c.Chip == "9999");
        }

        // ---- Raw reads: delete selected reads ----

        [AvaloniaTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Deleting_reads_only_happens_when_confirmed(bool yes)
        {
            Event e = AddEvent(makeCurrent: true);
            Db.AddChipReads([NewRead(e.Identifier, "1001", 60), NewRead(e.Identifier, "1002", 120)]);
            TimingPage timing = new(MainWindow, Db);
            Host(timing);
            TimingRawReadsPage page = new(timing, Db, MainWindow);
            Find<ContentControl>(timing, "TimingFrame").Content = page;
            Pump();
            ListBox list = Find<ListBox>(page, "UpdateListView");
            await WaitUntil(() => list.ItemsSource?.Cast<ChipRead>().Count() == 2, because: "reads to load");
            list.SelectedItems!.Add(list.ItemsSource!.Cast<ChipRead>().Single(r => r.ChipNumber == "1001"));
            int notified = MainWindow.NotifyTimingWorkerCalls;

            Click(Find<Button>(page, "DeleteButton"));
            Answer(await WaitForDialog("delete these records"), yes);

            Assert.Equal(yes ? ["1002"] : ["1001", "1002"], Db.GetChipReads(e.Identifier).Select(r => r.ChipNumber).Order());
            Assert.Equal(notified + (yes ? 1 : 0), MainWindow.NotifyTimingWorkerCalls);
        }

        // ---- Participant editor: bib already taken ----

        [AvaloniaTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Changing_to_a_taken_bib_swaps_bibs_only_when_confirmed(bool yes)
        {
            Event e = AddEvent(makeCurrent: true);
            Distance d = AddDistance(e.Identifier);
            Participant ann = AddParticipant(e.Identifier, d, "1", first: "Ann");
            AddParticipant(e.Identifier, d, "2", first: "Ben");
            ModifyParticipantWindow window = Show(ModifyParticipantWindow.NewWindow(MainWindow, Db, ann));
            Find<TextBox>(window, "BibBox").Text = "2";

            Click(Find<Button>(window, "Add"));
            Answer(await WaitForDialog("Swap bibs"), yes);

            Dictionary<string, string> bibs = Db.GetParticipants(e.Identifier).ToDictionary(p => p.FirstName, p => p.Bib);
            Assert.Equal(yes ? "2" : "1", bibs["Ann"]);
            Assert.Equal(yes ? "1" : "2", bibs["Ben"]);
            Assert.Equal(!yes, window.IsVisible);
        }

        [AvaloniaFact]
        public async Task Adding_with_a_taken_bib_moves_the_bib_to_the_new_person_when_confirmed()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance d = AddDistance(e.Identifier);
            AddParticipant(e.Identifier, d, "7", first: "Ann");
            ModifyParticipantWindow window = Show(ModifyParticipantWindow.NewWindow(MainWindow, Db));
            Find<TextBox>(window, "BibBox").Text = "7";
            Find<TextBox>(window, "FirstBox").Text = "Cat";
            Find<TextBox>(window, "LastBox").Text = "Charlie";

            Click(Find<Button>(window, "Add"));
            Answer(await WaitForDialog("Assign no bib to the previous bib owner"), true);

            Dictionary<string, string> bibs = Db.GetParticipants(e.Identifier).ToDictionary(p => p.FirstName, p => p.Bib);
            Assert.Equal("7", bibs["Cat"]);
            Assert.Equal("", bibs["Ann"]);
        }

        // ---- Settings: reset the database ----

        [AvaloniaTheory]
        [InlineData("ResetDb", true)]
        [InlineData("RebuildDb", true)]
        [InlineData("ResetDb", false)]
        public async Task Reset_and_rebuild_clear_everything_only_when_confirmed(string button, bool yes)
        {
            SeedEvent();
            SettingsPage page = new(MainWindow, Db);
            Host(page);

            Click(Find<Button>(page, button));
            Answer(await WaitForDialog("cannot recover"), yes);
            await Settle();

            Assert.Equal(yes ? 0 : 1, Db.GetEvents().Count);
        }

        // ---- Messages ----

        [AvaloniaFact]
        public async Task Connecting_a_reader_with_a_bad_address_says_why()
        {
            AddEvent(makeCurrent: true);
            TimingPage page = new(MainWindow, Db);
            Host(page);
            ReaderPart row = Find<ListBox>(page, "ReadersBox").Items.Cast<ReaderPart>().First();
            Find<TextBox>(row, "ReaderIp").Text = "192.168.1";

            Click(Find<Button>(row, "ConnectButton"));

            DialogBox message = await WaitForDialog("IP address given not valid");
            Assert.False(Find<Button>(message, "LeftButton").IsVisible);
            Answer(message, false);
            Assert.Empty(OpenDialogs());
        }
    }

    /// <summary>Reader and remote API actions that ask first, answered through the real message box.</summary>
    public class ReaderConfirmationTests : ReaderUiTestBase
    {
        public ReaderConfirmationTests() => AllowDialogs();

        private (ChronokeepInterface Portal, ChronokeepSettings Window) OpenPortalSettings()
        {
            (_, ChronokeepInterface portal) = Portal();
            portal.OpenSettings();
            Pump();
            ChronokeepSettings window = AddedWindow<ChronokeepSettings>();
            Assert.Equal("settings_get_all", PortalSent().GetProperty("command").GetString());
            return (portal, window);
        }

        [AvaloniaTheory]
        [InlineData("UpdateServerButton", "update the portal", "update")]
        [InlineData("RestartServerButton", "restart the portal", "restart")]
        [InlineData("StopServerButton", "stop the portal", "quit")]
        [InlineData("ShutdownServerButton", "shutdown the entire computer", "shutdown")]
        public async Task Portal_server_commands_are_sent_and_close_settings_when_confirmed(string button, string question, string command)
        {
            (ChronokeepInterface portal, ChronokeepSettings window) = OpenPortalSettings();

            Click(Find<Button>(window, button));
            Answer(await WaitForDialog(question), true);

            Assert.Equal(command, PortalSent().GetProperty("command").GetString());
            Assert.False(window.IsVisible);
            Assert.True(portal.WasShutdown());
        }

        [AvaloniaTheory]
        [InlineData("UpdateServerButton", "update the portal")]
        [InlineData("ShutdownServerButton", "shutdown the entire computer")]
        [InlineData("DeleteReadsButton", "delete all of the chip reads")]
        public async Task Declined_portal_commands_send_nothing(string button, string question)
        {
            (_, ChronokeepSettings window) = OpenPortalSettings();

            Click(Find<Button>(window, button));
            Answer(await WaitForDialog(question), false);

            Assert.True(Pair.NothingSent());
            Assert.True(window.IsVisible);
        }

        [AvaloniaFact]
        public async Task Clear_reads_deletes_the_portals_reads_when_confirmed()
        {
            (_, ChronokeepSettings window) = OpenPortalSettings();

            Click(Find<Button>(window, "DeleteReadsButton"));
            Answer(await WaitForDialog("delete all of the chip reads"), true);

            Assert.Equal("reads_delete_all", PortalSent().GetProperty("command").GetString());
            Assert.True(window.IsVisible);
        }

        [AvaloniaTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Saving_an_ultra_id_reboots_the_reader_only_when_confirmed(bool yes)
        {
            (_, RfidUltraInterface ultra) = Ultra();
            ultra.OpenSettings();
            Pump();
            RfidSettings window = AddedWindow<RfidSettings>();
            Assert.Equal("?\nU\n", System.Text.Encoding.Latin1.GetString(Pair.ReadLines(2)));
            Find<Slider>(window, "IdSlider").Value = 42;

            Click(Find<Button>(window, "SaveId"));
            Answer(await WaitForDialog("reboot the reader"), yes);

            if (yes)
            {
                Assert.Equal($"u{RfidUltraCodes.ULTRA_ID}42{(char)0xFF}\n", Pair.Line());
            }
            else
            {
                Assert.True(Pair.NothingSent());
            }
        }

        [AvaloniaFact]
        public async Task Saving_a_portal_reader_with_a_bad_address_says_why()
        {
            (ChronokeepInterface portal, ChronokeepSettings window) = OpenPortalSettings();
            portal.ParseMessages("""{"command":"readers","readers":[{"id":1,"name":"Zebra A","kind":"ZEBRA","ip_address":"10.0.0.20","port":5084,"auto_connect":true,"reading":false,"connected":false,"antennas":[]}]}""" + "\n", Pair.Client);
            Pump();
            var row = Find<ListBox>(window, "ReaderListView").Items.Cast<Chronokeep.UI.Timing.ReaderSettings.Parts.ReaderSubPart>().Single();
            Find<TextBox>(row, "IpBox").Text = "10.0.0";

            Click(Find<Button>(row, "SaveReaderButton"));

            Answer(await WaitForDialog("IP address given not valid"), false);
            Assert.True(Pair.NothingSent());
        }
    }

    /// <summary>Deleting data stored on a results or remote API, answered through the real message box.</summary>
    public class ApiConfirmationTests : UiTestBase
    {
        private readonly FakeApiServer server = new();

        public ApiConfirmationTests() => AllowDialogs();

        public override void Dispose()
        {
            base.Dispose();
            server.Dispose();
        }

        [AvaloniaTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Delete_uploaded_participants_only_runs_when_confirmed(bool yes)
        {
            Event e = AddEvent(makeCurrent: true);
            AddParticipant(e.Identifier, AddDistance(e.Identifier), "1");
            e = Db.GetCurrentEvent()!;
            e.ApiId = Db.AddApi(new ApiObject(0, CHRONOKEEP_RESULTS_SELF, server.Url, "Local", "tok", ""));
            e.ApiEventId = "spring-fling,2026";
            Db.UpdateEvent(e);
            server.Responder = _ => (HttpStatusCode.OK, """{"count":1}""");
            ParticipantsPage page = new(MainWindow, Db);
            Host(page);
            page.UpdateView();
            await Settle(100);

            Click(Find<Button>(page, "Delete"));
            Answer(await WaitForDialog("delete all participants loaded to the API"), yes);
            if (yes)
            {
                await WaitUntil(() => ServerRequests.Of(server).Count == 2, because: "both deletes");
            }
            else
            {
                await Settle();
            }

            Assert.Equal(yes ? ["participants/delete", "bibchips/delete"] : [], ServerRequests.Of(server).Select(r => r.Path));
        }

        [AvaloniaTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Deleting_remote_reads_only_runs_when_confirmed(bool yes)
        {
            AddEvent(makeCurrent: true);
            Db.AddApi(new ApiObject(0, CHRONOKEEP_REMOTE_SELF, server.Url, "Remote", "tok", ""));
            server.Responder = request => request.Path == "readers"
                ? (HttpStatusCode.OK, """{"readers":[{"name":"Finish A"}]}""")
                : (HttpStatusCode.OK, """{"count":3}""");
            RemoteReadersWindow window = Show(RemoteReadersWindow.CreateWindow(MainWindow, Db));
            await WaitUntil(() => !Find<StackPanel>(window, "LoadingPanel").IsVisible, because: "the readers to load");
            ReaderListItem reader = Find<ListBox>(window, "ApiListView").Items.Cast<ApiExpanderPart>()
                .SelectMany(a => Find<ListBox>(a, "ReaderListView").Items.Cast<ReaderListItem>()).Single();

            Click(Find<Button>(reader, "DeleteButton"));
            Answer(await WaitForDialog("delete every read uploaded"), yes);
            if (yes)
            {
                Answer(await WaitForDialog("Successfully deleted"), false);
            }
            else
            {
                await Settle();
            }

            Assert.Equal(yes ? 1 : 0, ServerRequests.Of(server).Count(r => r.Path == "reads/delete"));
        }
    }
}
