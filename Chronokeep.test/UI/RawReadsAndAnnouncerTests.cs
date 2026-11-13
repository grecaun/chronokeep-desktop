using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.Announcer;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.MainPages.Timing;
using Chronokeep.UI.Timing.Windows;

namespace Chronokeep.Test.UI
{
    /// <summary>The timing Raw Reads page, the Edit Raw Reads (shift) window, and the Announcer window.</summary>
    public class RawReadsAndAnnouncerTests : UiTestBase
    {
        public RawReadsAndAnnouncerTests() => Globals.UpdateIgnoredChips(Db);

        public override void Dispose()
        {
            Db.RemoveBibChipAssociations(Db.GetBibChips(-1));
            Globals.UpdateIgnoredChips(Db);
            base.Dispose();
        }

        private Event SeedReads()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance d = AddDistance(e.Identifier);
            AddParticipant(e.Identifier, d, "1");
            Db.AddChipReads([
                NewRead(e.Identifier, "1001", 60),
                NewRead(e.Identifier, "1002", 120),
                new ChipRead(e.Identifier, Constants.Timing.LOCATION_FINISH, "1", new DateTime(2026, 6, 15, 8, 3, 0), Constants.Timing.CHIPREAD_STATUS_NONE),
            ]);
            return Db.GetCurrentEvent()!;
        }

        private TimingRawReadsPage OpenRawReads(out TimingPage timing)
        {
            timing = new TimingPage(MainWindow, Db);
            Host(timing);
            TimingRawReadsPage page = new(timing, Db, MainWindow);
            Find<ContentControl>(timing, "TimingFrame").Content = page;
            Pump();
            return page;
        }

        private static List<ChipRead> Listed(TimingRawReadsPage page) =>
            [.. Find<ListBox>(page, "UpdateListView").ItemsSource!.Cast<ChipRead>()];

        private static void Select(TimingRawReadsPage page, Func<ChipRead, bool> which)
        {
            ListBox list = Find<ListBox>(page, "UpdateListView");
            list.SelectedItems!.Clear();
            foreach (ChipRead read in Listed(page).Where(which))
            {
                list.SelectedItems.Add(read);
            }
            Pump();
        }

        private int StatusOf(Event e, string chipOrBib) =>
            Db.GetChipReads(e.Identifier).Single(r => r.ChipNumber == chipOrBib || r.ReadBib == chipOrBib).Status;

        [AvaloniaFact]
        public void Raw_reads_lists_every_read_for_the_event()
        {
            SeedReads();

            Assert.Equal(3, Listed(OpenRawReads(out _)).Count);
        }

        [AvaloniaFact]
        public void Ignore_toggles_a_read_on_and_off()
        {
            Event e = SeedReads();
            TimingRawReadsPage page = OpenRawReads(out _);

            Select(page, r => r.ChipNumber == "1001");
            Click(Find<Button>(page, "IgnoreButton"));
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_IGNORE, StatusOf(e, "1001"));
            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);

            Select(page, r => r.ChipNumber == "1001");
            Click(Find<Button>(page, "IgnoreButton"));
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_NONE, StatusOf(e, "1001"));
        }

        [AvaloniaFact]
        public void Ignoring_a_dnf_read_can_be_undone_back_to_dnf()
        {
            Event e = SeedReads();
            TimingRawReadsPage page = OpenRawReads(out _);

            Select(page, r => r.ChipNumber == "1002");
            Click(Find<Button>(page, "ChangeDnf"));
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_DNF, StatusOf(e, "1002"));

            Select(page, r => r.ChipNumber == "1002");
            Click(Find<Button>(page, "IgnoreButton"));
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_DNF_IGNORE, StatusOf(e, "1002"));

            Select(page, r => r.ChipNumber == "1002");
            Click(Find<Button>(page, "IgnoreButton"));
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_DNF, StatusOf(e, "1002"));
        }

        [AvaloniaFact]
        public void Dns_toggles_a_read_on_and_off()
        {
            Event e = SeedReads();
            TimingRawReadsPage page = OpenRawReads(out _);

            Select(page, r => r.ChipNumber == "1001");
            Click(Find<Button>(page, "ChangeDns"));
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_DNS, StatusOf(e, "1001"));

            Select(page, r => r.ChipNumber == "1001");
            Click(Find<Button>(page, "ChangeDns"));
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_NONE, StatusOf(e, "1001"));
        }

        [AvaloniaFact]
        public void Buttons_do_nothing_when_no_read_is_selected()
        {
            Event e = SeedReads();
            TimingRawReadsPage page = OpenRawReads(out _);
            Select(page, _ => false);

            Click(Find<Button>(page, "IgnoreButton"));
            Click(Find<Button>(page, "ChangeDns"));
            Click(Find<Button>(page, "ChangeDnf"));
            Click(Find<Button>(page, "GlobalIgnore"));

            Assert.All(Db.GetChipReads(e.Identifier), r => Assert.Equal(Constants.Timing.CHIPREAD_STATUS_NONE, r.Status));
            Assert.Equal(0, MainWindow.NotifyTimingWorkerCalls);
            Assert.Empty(Db.GetBibChips(-1));
        }

        [AvaloniaFact]
        public void Filters_show_only_ignored_or_only_manual_reads()
        {
            SeedReads();
            TimingRawReadsPage page = OpenRawReads(out _);
            Select(page, r => r.ChipNumber == "1001");
            Click(Find<Button>(page, "IgnoreButton"));

            Find<ToggleSwitch>(page, "OnlyIgnoreBox").IsChecked = true;
            Pump();
            Assert.Equal("1001", Assert.Single(Listed(page)).ChipNumber);

            Find<ToggleSwitch>(page, "OnlyIgnoreBox").IsChecked = false;
            Find<ToggleSwitch>(page, "OnlyManualBox").IsChecked = true;
            Pump();
            Assert.Equal(Constants.Timing.CHIPREAD_TYPE_MANUAL, Assert.Single(Listed(page)).Type);
        }

        [AvaloniaFact]
        public void Ignore_chip_adds_chips_but_not_manual_entries_or_repeats()
        {
            SeedReads();
            TimingRawReadsPage page = OpenRawReads(out _);

            Select(page, _ => true);
            Click(Find<Button>(page, "GlobalIgnore"));
            Select(page, r => r.ChipNumber == "1001");
            Click(Find<Button>(page, "GlobalIgnore"));

            Assert.Equal(["1001", "1002"], Db.GetBibChips(-1).Select(c => c.Chip).Order());
            Assert.Equal(["1001", "1002"], Globals.GetIgnoredChips().Select(c => c.Chip).Order());
        }

        [AvaloniaFact]
        public void Done_returns_to_the_results_view()
        {
            SeedReads();
            TimingRawReadsPage page = OpenRawReads(out TimingPage timing);

            Click(Find<Button>(page, "DoneButton"));

            Assert.IsType<TimingResultsPage>(Find<ContentControl>(timing, "TimingFrame").Content);
        }

        private EditRawReadsWindow OpenShift(Event e, out TimingPage timing, params string[] chips)
        {
            timing = new TimingPage(MainWindow, Db);
            Host(timing);
            List<ChipRead> reads = [.. Db.GetChipReads(e.Identifier).Where(r => chips.Contains(r.ChipNumber))];
            return Show(new EditRawReadsWindow(timing, Db, reads));
        }

        private DateTime TimeOf(Event e, string chip) => Db.GetChipReads(e.Identifier).Single(r => r.ChipNumber == chip).Time;

        [AvaloniaFact]
        public void Shift_adds_time_to_the_chosen_reads_only()
        {
            Event e = SeedReads();
            DateTime before1001 = TimeOf(e, "1001"), before1002 = TimeOf(e, "1002");
            EditRawReadsWindow window = OpenShift(e, out _, "1001");

            Find<MaskedTextBox>(window, "TimeBox").Text = "00:01:30.500";
            Click(Find<Button>(window, "Submit"));

            Assert.Equal(before1001.AddSeconds(90.5), TimeOf(e, "1001"));
            Assert.Equal(before1002, TimeOf(e, "1002"));
            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);
            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public void Shift_can_subtract_time_and_whole_days()
        {
            Event e = SeedReads();
            DateTime before = TimeOf(e, "1001");
            EditRawReadsWindow window = OpenShift(e, out _, "1001");

            Find<RadioButton>(window, "SubRadio").IsChecked = true;
            Find<TextBox>(window, "DaysBox").Text = "1";
            Find<MaskedTextBox>(window, "TimeBox").Text = "00:00:00.250";
            Click(Find<Button>(window, "Submit"));

            Assert.Equal(before.AddDays(-1).AddMilliseconds(-250), TimeOf(e, "1001"));
        }

        [AvaloniaFact]
        public void Shift_cancel_changes_nothing()
        {
            Event e = SeedReads();
            DateTime before = TimeOf(e, "1001");
            EditRawReadsWindow window = OpenShift(e, out _, "1001");
            Find<MaskedTextBox>(window, "TimeBox").Text = "00:05:00.000";

            Click(Find<Button>(window, "Cancel"));

            Assert.Equal(before, TimeOf(e, "1001"));
            Assert.Equal(0, MainWindow.NotifyTimingWorkerCalls);
        }

        /// <summary>
        /// A finish result tied to a real chip read at <paramref name="systemTime"/>; saved results take
        /// their system time from their read, which is what the announcer's time window checks.
        /// </summary>
        private TimeResult Finish(Event e, Participant p, DateTime systemTime, int segment = Constants.Timing.SEGMENT_FINISH, int status = Constants.Timing.TIMERESULT_STATUS_NONE)
        {
            Db.AddChipReads([new ChipRead(e.Identifier, Constants.Timing.LOCATION_FINISH, p.Bib, systemTime, Constants.Timing.CHIPREAD_STATUS_NONE)]);
            int readId = Db.GetChipReads(e.Identifier).Single(r => r.ReadBib == p.Bib).ReadId;
            return new(e.Identifier, readId, p.EventSpecific.Identifier,
                Constants.Timing.LOCATION_FINISH, segment, 1, 1800, 0, "", 1800, 0, systemTime, p.Bib, status, "");
        }

        private static System.Collections.IList AnnouncerList() =>
            (System.Collections.IList)typeof(Chronokeep.Timing.Announcer.AnnouncerWorker)
                .GetField("Participants", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;

        private static AnnouncerParticipant SeenAt(string bib, Participant? p, int secondsAgo) =>
            new(bib, p, Constants.Timing.RfidDateToEpoch(DateTime.Now.AddSeconds(-secondsAgo)));

        private static List<string> Bibs(AnnouncerWindow window) =>
            [.. Find<DataGrid>(window, "AnnouncerBox").ItemsSource!.Cast<AnnouncerParticipant>().Select(r => r.Bib)];

        /// <summary>Opens the announcer window with the given announcer-location rows in the worker's list.</summary>
        private AnnouncerWindow OpenAnnouncer(params AnnouncerParticipant[] announced)
        {
            System.Collections.IList list = AnnouncerList();
            list.Clear();
            foreach (AnnouncerParticipant a in announced) list.Add(a);
            return Show(new AnnouncerWindow(MainWindow, Db));
        }

        [AvaloniaFact]
        public void Announcer_shows_recent_finishers_newest_first()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance d = AddDistance(e.Identifier);
            Participant first = AddParticipant(e.Identifier, d, "1", first: "First");
            Participant second = AddParticipant(e.Identifier, d, "2", first: "Second");
            Participant old = AddParticipant(e.Identifier, d, "3", first: "Old");
            Participant dnf = AddParticipant(e.Identifier, d, "4", first: "Dnf");
            Db.AddTimingResults([
                Finish(e, first, DateTime.Now.AddSeconds(-20)),
                Finish(e, second, DateTime.Now.AddSeconds(-5)),
                Finish(e, old, DateTime.Now.AddMinutes(-30)),
                Finish(e, dnf, DateTime.Now.AddSeconds(-2), status: Constants.Timing.TIMERESULT_STATUS_DNF),
            ]);

            AnnouncerWindow window = OpenAnnouncer();

            Assert.Equal(["2", "1"], Bibs(window));
            Assert.Equal("First Runner", Find<DataGrid>(window, "AnnouncerBox").ItemsSource!.Cast<AnnouncerParticipant>().Last().ParticipantName);
            Assert.Equal(1, MainWindow.StartAnnouncerWorkerCalls);
        }

        [AvaloniaFact]
        public void Announcer_lists_announcer_reads_and_finishers_together_once_each()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance d = AddDistance(e.Identifier);
            Participant passing = AddParticipant(e.Identifier, d, "1", first: "Passing");
            Participant finished = AddParticipant(e.Identifier, d, "2", first: "Finished");
            Participant both = AddParticipant(e.Identifier, d, "3", first: "Both");
            Db.AddTimingResults([
                Finish(e, finished, DateTime.Now.AddSeconds(-10)),
                Finish(e, both, DateTime.Now.AddSeconds(-3)),
            ]);
            try
            {
                AnnouncerWindow window = OpenAnnouncer(SeenAt("1", passing, 5), SeenAt("3", both, 30));

                Assert.Equal(["3", "1", "2"], Bibs(window));
            }
            finally
            {
                AnnouncerList().Clear();
            }
        }

        [AvaloniaFact]
        public void Show_unknown_participants_switch_adds_bibs_nobody_is_registered_with()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance d = AddDistance(e.Identifier);
            Participant known = AddParticipant(e.Identifier, d, "1", first: "Known");
            try
            {
                AnnouncerWindow window = OpenAnnouncer(SeenAt("1", known, 10), SeenAt("77", null, 5));
                Assert.Equal(["1"], Bibs(window));

                Find<ToggleSwitch>(window, "ShowUnknownSwitch").IsChecked = true;
                Pump();
                Assert.Equal(["77", "1"], Bibs(window));

                Find<ToggleSwitch>(window, "ShowUnknownSwitch").IsChecked = false;
                Pump();
                Assert.Equal(["1"], Bibs(window));
            }
            finally
            {
                AnnouncerList().Clear();
            }
        }

        [AvaloniaFact]
        public void Closing_the_announcer_tells_the_main_window()
        {
            AddEvent(makeCurrent: true);
            AnnouncerWindow window = Show(new AnnouncerWindow(MainWindow, Db));

            window.Close();
            Pump();

            Assert.Equal(1, MainWindow.AnnouncerClosingCalls);
        }
    }
}
