using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.MainPages.Timing;
using Chronokeep.UI.Parts;

namespace Chronokeep.Test.UI
{
    /// <summary>The Chip Assignment page (bib/chip links and ignored chips) and the timing Alarms page.</summary>
    public class ChipAssignmentAndAlarmsTests : UiTestBase
    {
        public ChipAssignmentAndAlarmsTests()
        {
            // Ignored chips and alarms live in process-wide lists; start each test empty.
            Globals.UpdateIgnoredChips(Db);
            Alarm.ClearAlarms();
        }

        public override void Dispose()
        {
            Alarm.ClearAlarms();
            Db.RemoveBibChipAssociations(Db.GetBibChips(-1));
            Globals.UpdateIgnoredChips(Db);
            base.Dispose();
        }

        private async Task<ChipAssignmentPage> OpenChips()
        {
            ChipAssignmentPage page = new(MainWindow, Db);
            Host(page);
            await WaitUntil(() => Find<DataGrid>(page, "BibChipList").ItemsSource != null, because: "chips to load");
            await Settle(100);
            return page;
        }

        private static List<BibChipAssociation> Listed(ChipAssignmentPage page, string list = "BibChipList") =>
            [.. Find<DataGrid>(page, list).ItemsSource!.Cast<BibChipAssociation>()];

        private List<(string Bib, string Chip)> Stored(int eventId) =>
            [.. Db.GetBibChips(eventId).Select(b => (b.Bib, b.Chip)).OrderBy(b => b.Bib.PadLeft(6))];

        private void AddChips(int eventId, params (string Bib, string Chip)[] chips) =>
            Db.AddBibChipAssociation(eventId, [.. chips.Select(c => new BibChipAssociation { EventId = eventId, Bib = c.Bib, Chip = c.Chip })]);

        [AvaloniaFact]
        public async Task Lists_the_events_chips_and_suggests_the_next_chip()
        {
            Event e = AddEvent(makeCurrent: true);
            AddChips(e.Identifier, ("1", "1001"), ("2", "1002"));

            ChipAssignmentPage page = await OpenChips();

            Assert.Equal(2, Listed(page).Count);
            Assert.Equal("1003", Find<TextBox>(page, "SingleChipBox").Text);
            Assert.Equal("1003", Find<TextBox>(page, "RangeStartChipBox").Text);
        }

        [AvaloniaFact]
        public async Task Saving_a_single_link_stores_it_and_moves_to_the_next_bib()
        {
            Event e = AddEvent(makeCurrent: true);
            ChipAssignmentPage page = await OpenChips();
            Find<TextBox>(page, "SingleBibBox").Text = "7";
            Find<TextBox>(page, "SingleChipBox").Text = "5007";

            Click(Find<Button>(page, "SaveSingleButton"));
            await WaitUntil(() => Listed(page).Count == 1, because: "the list to refresh");

            Assert.Equal([("7", "5007")], Stored(e.Identifier));
            Assert.Equal("8", Find<TextBox>(page, "SingleBibBox").Text);
        }

        [AvaloniaFact]
        public async Task Hex_chips_are_saved_and_suggested_in_hex()
        {
            Event e = AddEvent(makeCurrent: true);
            Db.SetAppSetting(Constants.Settings.DEFAULT_CHIP_TYPE, Constants.Settings.CHIP_TYPE_HEX);
            AddChips(e.Identifier, ("1", "FF"));
            ChipAssignmentPage page = await OpenChips();
            Assert.Equal("100", Find<TextBox>(page, "SingleChipBox").Text);

            Find<TextBox>(page, "SingleBibBox").Text = "2";
            Click(Find<Button>(page, "SaveSingleButton"));

            Assert.Contains(("2", "100"), Stored(e.Identifier));
        }

        [AvaloniaFact]
        public async Task A_range_links_each_bib_to_the_next_chip()
        {
            Event e = AddEvent(makeCurrent: true);
            ChipAssignmentPage page = await OpenChips();
            Find<TextBox>(page, "RangeStartChipBox").Text = "2000";
            Find<TextBox>(page, "RangeStartBibBox").Text = "10";
            Find<TextBox>(page, "RangeEndBibBox").Text = "12";
            Pump();
            Assert.Equal("2002", Find<TextBlock>(page, "RangeEndChipLabel").Text);

            Click(Find<Button>(page, "SaveRangeButton"));

            Assert.Equal([("10", "2000"), ("11", "2001"), ("12", "2002")], Stored(e.Identifier));
            Assert.Equal("13", Find<TextBox>(page, "RangeStartBibBox").Text);
        }

        [AvaloniaFact]
        public async Task A_reverse_range_counts_bibs_down_while_chips_count_up()
        {
            Event e = AddEvent(makeCurrent: true);
            ChipAssignmentPage page = await OpenChips();
            Find<TextBox>(page, "RangeStartChipBox").Text = "2000";
            Find<TextBox>(page, "RangeStartBibBox").Text = "12";
            Find<TextBox>(page, "RangeEndBibBox").Text = "10";
            Pump();

            Click(Find<Button>(page, "SaveRangeButton"));

            Assert.Equal([("10", "2002"), ("11", "2001"), ("12", "2000")], Stored(e.Identifier));
        }

        [AvaloniaFact]
        public async Task Delete_removes_the_selected_links_and_recalculates_when_leaving()
        {
            Event e = AddEvent(makeCurrent: true);
            AddChips(e.Identifier, ("1", "1001"), ("2", "1002"));
            ChipAssignmentPage page = await OpenChips();
            DataGrid list = Find<DataGrid>(page, "BibChipList");
            list.SelectedItem = Listed(page).Single(b => b.Bib == "1");

            Click(Find<Button>(page, "Delete"));
            page.Closing();

            Assert.Equal([("2", "1002")], Stored(e.Identifier));
            Assert.Equal(1, MainWindow.NetworkClearResultsCalls);
        }

        [AvaloniaFact]
        public async Task Delete_with_nothing_selected_changes_nothing()
        {
            Event e = AddEvent(makeCurrent: true);
            AddChips(e.Identifier, ("1", "1001"));
            ChipAssignmentPage page = await OpenChips();

            Click(Find<Button>(page, "Delete"));
            page.Closing();

            Assert.Single(Stored(e.Identifier));
            Assert.Equal(0, MainWindow.NetworkClearResultsCalls);
        }

        [AvaloniaFact]
        public async Task Ignored_chips_are_added_and_removed_from_what_timing_ignores()
        {
            AddEvent(makeCurrent: true);
            ChipAssignmentPage page = await OpenChips();

            Find<TextBox>(page, "IgnoredChipBox").Text = "9999";
            Click(Find<Button>(page, "SaveIgnored"));
            await WaitUntil(() => Listed(page, "IgnoredChipList").Count == 1, because: "the ignored list to refresh");
            Assert.Contains(Globals.GetIgnoredChips(), c => c.Chip == "9999");

            Find<DataGrid>(page, "IgnoredChipList").SelectedItem = Listed(page, "IgnoredChipList").Single();
            Click(Find<Button>(page, "DeleteIgnored"));

            Assert.Empty(Db.GetBibChips(-1));
            Assert.DoesNotContain(Globals.GetIgnoredChips(), c => c.Chip == "9999");
        }

        [AvaloniaFact]
        public async Task Copy_brings_over_a_previous_events_chips()
        {
            Event old = AddEvent("Last Year", DateString(2025));
            AddChips(old.Identifier, ("1", "1001"), ("2", "1002"));
            Event e = AddEvent("This Year", DateString(2026), makeCurrent: true);
            ChipAssignmentPage page = await OpenChips();
            ComboBox previous = Find<ComboBox>(page, "PreviousEvents");
            previous.SelectedItem = previous.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag! == old.Identifier.ToString());

            Click(Find<Button>(page, "Copy"));

            Assert.Equal([("1", "1001"), ("2", "1002")], Stored(e.Identifier));
            Assert.Equal(2, Stored(old.Identifier).Count);
        }

        [AvaloniaFact]
        public async Task Changing_the_chip_type_is_remembered()
        {
            AddEvent(makeCurrent: true);
            ChipAssignmentPage page = await OpenChips();

            Find<ComboBox>(page, "ChipTypeBox").SelectedIndex = 1;
            Pump();

            Assert.Equal(Constants.Settings.CHIP_TYPE_HEX, Db.GetAppSetting(Constants.Settings.DEFAULT_CHIP_TYPE)!.Value);
        }

        private AlarmsPage OpenAlarms(out TimingPage timing)
        {
            timing = new TimingPage(MainWindow, Db);
            Host(timing);
            AlarmsPage page = new(timing, Db);
            Host(page);
            return page;
        }

        private static List<AlarmPart> AlarmRows(AlarmsPage page) =>
            [.. Find<ListBox>(page, "AlarmsBox").Items.OfType<AlarmPart>()];

        private static void SetAlarm(AlarmPart row, string bib, string chip = "")
        {
            Find<TextBox>(row, "BibBox").Text = bib;
            Find<TextBox>(row, "ChipBox").Text = chip;
        }

        [AvaloniaFact]
        public void Alarms_page_shows_the_current_alarms()
        {
            Event e = AddEvent(makeCurrent: true);
            Alarm.AddAlarms([new Alarm(-1, "5", "", true, 1), new Alarm(-1, "", "1234", false, 0)]);

            List<AlarmPart> rows = AlarmRows(OpenAlarms(out _));

            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, r => Find<TextBox>(r, "BibBox").Text == "5");
            Assert.Contains(rows, r => Find<TextBox>(r, "ChipBox").Text == "1234");
        }

        [AvaloniaFact]
        public void Saving_stores_new_alarms_and_a_bib_alarm_drops_its_chip()
        {
            Event e = AddEvent(makeCurrent: true);
            AlarmsPage page = OpenAlarms(out _);
            Click(Find<Button>(page, "AddButton"));
            Click(Find<Button>(page, "AddButton"));
            SetAlarm(AlarmRows(page)[0], "42", "should-be-dropped");
            SetAlarm(AlarmRows(page)[1], "", "7777");

            Click(Find<Button>(page, "SaveButton"));

            List<Alarm> saved = Db.GetAlarms(e.Identifier);
            Assert.Equal(2, saved.Count);
            Assert.Equal("", saved.Single(a => a.Bib == "42").Chip);
            Assert.Contains(saved, a => a.Chip == "7777");
            Assert.NotNull(Alarm.GetAlarmByBib("42"));
        }

        [AvaloniaFact]
        public void Duplicate_bibs_are_not_saved()
        {
            Event e = AddEvent(makeCurrent: true);
            AlarmsPage page = OpenAlarms(out _);
            Click(Find<Button>(page, "AddButton"));
            Click(Find<Button>(page, "AddButton"));
            SetAlarm(AlarmRows(page)[0], "42");
            SetAlarm(AlarmRows(page)[1], "42");

            Click(Find<Button>(page, "SaveButton"));

            Assert.Empty(Db.GetAlarms(e.Identifier));
        }

        [AvaloniaFact]
        public void Only_one_alarm_without_a_bib_or_chip_is_allowed()
        {
            Event e = AddEvent(makeCurrent: true);
            AlarmsPage page = OpenAlarms(out _);
            Click(Find<Button>(page, "AddButton"));
            Click(Find<Button>(page, "AddButton"));

            Click(Find<Button>(page, "SaveButton"));

            Assert.Empty(Db.GetAlarms(e.Identifier));
        }

        [AvaloniaFact]
        public void Removing_an_alarm_deletes_it()
        {
            Event e = AddEvent(makeCurrent: true);
            Alarm.AddAlarm(new Alarm(-1, "5", "", true, 0));
            Alarm.SaveAlarms(e.Identifier, Db);
            AlarmsPage page = OpenAlarms(out _);

            Click(Find<Button>(AlarmRows(page).Single(), "RemoveButton"));

            Assert.Empty(AlarmRows(page));
            Assert.Empty(Db.GetAlarms(e.Identifier));
            Assert.Null(Alarm.GetAlarmByBib("5"));
        }

        [AvaloniaFact]
        public void Leaving_saves_valid_alarms_when_save_on_page_change_is_on()
        {
            Event e = AddEvent(makeCurrent: true);
            AlarmsPage page = OpenAlarms(out _);
            Click(Find<Button>(page, "AddButton"));
            SetAlarm(AlarmRows(page).Single(), "88");

            page.Closing();

            Assert.Equal("88", Assert.Single(Db.GetAlarms(e.Identifier)).Bib);
        }

        [AvaloniaFact]
        public void Done_returns_to_the_results_view()
        {
            AddEvent(makeCurrent: true);
            TimingPage timing = new(MainWindow, Db);
            Host(timing);
            AlarmsPage page = new(timing, Db);
            Find<ContentControl>(timing, "TimingFrame").Content = page;
            Pump();

            Click(Find<Button>(page, "DoneButton"));

            Assert.IsType<TimingResultsPage>(Find<ContentControl>(timing, "TimingFrame").Content);
        }
    }
}
