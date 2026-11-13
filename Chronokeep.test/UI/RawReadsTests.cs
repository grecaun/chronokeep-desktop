using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.MainPages.Timing;
using Chronokeep.UI.Timing.Windows;

namespace Chronokeep.Test.UI
{
    /// <summary>The timing Raw Reads page and the Edit Raw Reads (shift) window.</summary>
    public class RawReadsTests : UiTestBase
    {
        public RawReadsTests() => Globals.UpdateIgnoredChips(Db);

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
    }
}
