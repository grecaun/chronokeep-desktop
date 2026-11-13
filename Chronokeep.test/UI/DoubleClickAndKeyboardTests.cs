using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.ChipAssignment;
using Chronokeep.UI.ChipAssignment.Parts;
using Chronokeep.UI.EventWindows;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.MainPages.Timing;
using Chronokeep.UI.Participants;
using Chronokeep.UI.Parts;
using Chronokeep.UI.Timing.Windows;
using Chronokeep.UI.UhfRfidReader;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.UI
{
    /// <summary>Double-clicking a row opens or picks it, done with the headless mouse.</summary>
    public class DoubleClickTests : UiTestBase
    {
        private (Event Event, Participant Ann, Participant Cat) Seed()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            Distance tenK = AddDistance(e.Identifier, "10K");
            Participant ann = AddParticipant(e.Identifier, fiveK, "1", first: "Ann", last: "Alpha");
            Participant cat = AddParticipant(e.Identifier, tenK, "3", first: "Cat", last: "Charlie");
            return (Db.GetCurrentEvent()!, ann, cat);
        }

        private static TimeResult Finish(Event e, Participant p, long seconds) =>
            new(e.Identifier, TimingConstants.TIMERESULT_DUMMYREAD, p.EventSpecific.Identifier,
                TimingConstants.LOCATION_FINISH, TimingConstants.SEGMENT_FINISH, 1, seconds, 0, "", seconds, 0,
                DateTime.Now, p.Bib, TimingConstants.TIMERESULT_STATUS_NONE, "");

        [AvaloniaFact]
        public async Task Participants_page_opens_the_editor_for_the_double_clicked_person()
        {
            (_, _, Participant cat) = Seed();
            ShowMainWindow();
            ParticipantsPage page = new(MainWindow, Db);
            Host(page);
            page.UpdateView();
            DataGrid list = Find<DataGrid>(page, "ParticipantsList");
            await WaitUntil(() => list.ItemsSource?.Cast<Participant>().Count() == 2, because: "participants to load");
            await Settle();
            Participant listed = list.ItemsSource!.Cast<Participant>().Single(p => p.Bib == "3");

            DoubleClick(RowOf(list, listed));
            Assert.Equal("3", (list.SelectedItem as Participant)?.Bib);

            ModifyParticipantWindow editor = AddedWindow<ModifyParticipantWindow>();
            Assert.Equal("Cat", Find<TextBox>(editor, "FirstBox").Text);
            Assert.Equal("3", Find<TextBox>(editor, "BibBox").Text);
            Assert.Equal("Update", ContentText(Find<Button>(editor, "Add")));
        }

        [AvaloniaFact]
        public async Task Timing_results_open_the_editor_for_the_runner_with_their_bib_locked()
        {
            (Event e, Participant ann, _) = Seed();
            Db.AddTimingResults([Finish(e, ann, 1500)]);
            TimingPage page = new(MainWindow, Db);
            Window host = Host(page);
            ListBox results = Find<ListBox>((TimingResultsPage)Find<ContentControl>(page, "TimingFrame").Content!, "UpdateListView");
            await WaitUntil(() => results.ItemsSource?.Cast<TimeResult>().Count() == 1, because: "results to load");

            DoubleClick(RowOf(results, results.ItemsSource!.Cast<TimeResult>().Single()));

            ModifyParticipantWindow editor = host.OwnedWindows.OfType<ModifyParticipantWindow>().Single();
            Assert.Equal("Ann", Find<TextBox>(editor, "FirstBox").Text);
            Assert.False(Find<TextBox>(editor, "BibBox").IsEnabled);
            editor.Close();
        }

        [AvaloniaFact]
        public async Task Timing_stats_open_the_distance_stats_for_the_double_clicked_distance()
        {
            Seed();
            TimingPage page = new(MainWindow, Db);
            Host(page);
            await Settle();
            DataGrid stats = Find<DataGrid>(page, "StatsListView");
            DistanceStat tenK = stats.ItemsSource!.Cast<DistanceStat>().Single(s => s.DistanceName == "10K");

            DoubleClick(RowOf(stats, tenK));

            DistanceStatsPage statsPage = Assert.IsType<DistanceStatsPage>(Find<ContentControl>(page, "TimingFrame").Content);
            Assert.Equal("10K", Find<TextBlock>(statsPage, "DistanceName").Text);
        }

        [AvaloniaFact]
        public async Task Distance_stats_open_the_editor_for_the_double_clicked_runner()
        {
            (Event e, Participant ann, _) = Seed();
            ann.Status = TimingConstants.EVENTSPECIFIC_STARTED;
            Db.UpdateParticipants([ann]);
            ShowMainWindow();
            TimingPage page = new(MainWindow, Db);
            Host(page);
            await Settle();
            DataGrid stats = Find<DataGrid>(page, "StatsListView");
            DoubleClick(RowOf(stats, stats.ItemsSource!.Cast<DistanceStat>().Single(s => s.DistanceName == "5K")));
            DistanceStatsPage statsPage = Assert.IsType<DistanceStatsPage>(Find<ContentControl>(page, "TimingFrame").Content);
            DataGrid active = Find<DataGrid>(statsPage, "ActiveListView");
            await WaitUntil(() => active.ItemsSource?.Cast<object>().Count() == 1, because: "the active runner to load");

            DoubleClick(RowOf(active, active.ItemsSource!.Cast<object>().Single()));
            Assert.NotNull(active.SelectedItem);

            ModifyParticipantWindow editor = Assert.Single(MainWindow.OwnedWindows.OfType<ModifyParticipantWindow>());
            Assert.Equal("Ann", Find<TextBox>(editor, "FirstBox").Text);
            editor.Close();
        }

        [AvaloniaFact]
        public async Task Change_event_switches_to_the_double_clicked_event()
        {
            AddEvent("Spring Fling", makeCurrent: true);
            Event other = AddEvent("Harvest Half");
            ChangeEventWindow window = Show(ChangeEventWindow.NewWindow(MainWindow, Db));
            ListBox events = Find<ListBox>(window, "EventList");
            await WaitUntil(() => events.ItemsSource?.Cast<Event>().Count() == 2, because: "events to load");
            Event listed = events.ItemsSource!.Cast<Event>().Single(e => e.Name == "Harvest Half");

            DoubleClick(RowOf(events, listed));

            Assert.Equal(other.Identifier, Db.GetCurrentEvent()!.Identifier);
            Assert.False(window.IsVisible);
            Assert.True(MainWindow.WindowFinalizeCalls > 0);
        }
    }

    /// <summary>Enter and Escape shortcuts, pressed on the headless keyboard in the focused box.</summary>
    public class KeyboardShortcutTests : UiTestBase
    {
        private async Task<ChipAssignmentPage> OpenChips()
        {
            ChipAssignmentPage page = new(MainWindow, Db);
            Host(page);
            await WaitUntil(() => Find<DataGrid>(page, "BibChipList").ItemsSource != null, because: "chips to load");
            await Settle(100);
            return page;
        }

        [AvaloniaFact]
        public async Task Enter_in_the_single_chip_boxes_saves_the_pair()
        {
            Event e = AddEvent(makeCurrent: true);
            ChipAssignmentPage page = await OpenChips();
            Find<TextBox>(page, "SingleBibBox").Text = "7";
            Find<TextBox>(page, "SingleChipBox").Text = "5007";

            PressKey(Find<TextBox>(page, "SingleChipBox"), PhysicalKey.Enter);

            BibChipAssociation saved = Assert.Single(Db.GetBibChips(e.Identifier));
            Assert.Equal(("7", "5007"), (saved.Bib, saved.Chip));
        }

        [AvaloniaFact]
        public async Task Enter_in_the_range_boxes_saves_the_range()
        {
            Event e = AddEvent(makeCurrent: true);
            ChipAssignmentPage page = await OpenChips();
            Find<TextBox>(page, "RangeStartBibBox").Text = "10";
            Find<TextBox>(page, "RangeEndBibBox").Text = "12";
            Find<TextBox>(page, "RangeStartChipBox").Text = "2000";

            PressKey(Find<TextBox>(page, "RangeEndBibBox"), PhysicalKey.Enter);

            Assert.Equal(["10", "11", "12"], Db.GetBibChips(e.Identifier).Select(b => b.Bib).OrderBy(b => b.PadLeft(4)));
        }

        [AvaloniaFact]
        public async Task Enter_in_the_ignored_chip_box_ignores_the_chip()
        {
            AddEvent(makeCurrent: true);
            ChipAssignmentPage page = await OpenChips();
            Find<TextBox>(page, "IgnoredChipBox").Text = "9999";

            PressKey(Find<TextBox>(page, "IgnoredChipBox"), PhysicalKey.Enter);

            Assert.Contains(Db.GetBibChips(-1), c => c.Chip == "9999");
            Assert.Contains(Globals.GetIgnoredChips(), c => c.Chip == "9999");
        }

        [AvaloniaFact]
        public void Chip_tool_range_boxes_take_only_digits_and_can_be_corrected()
        {
            AddEvent(makeCurrent: true);
            ChipTool tool = Show(ChipTool.NewWindow(MainWindow, Db));
            Click(Find<Button>(tool, "AddRangeButton"));
            TagRangePart range = Find<ListBox>(tool, "CorrelationBox").Items.OfType<TagRangePart>().Last();
            TextBox startBib = Find<TextBox>(range, "StartBib");
            startBib.Text = "";

            Type(startBib, "12");
            Type(startBib, "x");
            Type(startBib, "3");
            Assert.Equal("123", startBib.Text);

            PressKey(startBib, PhysicalKey.Backspace);
            Assert.Equal("12", startBib.Text);
        }

        [AvaloniaFact]
        public void Reader_port_box_takes_only_digits()
        {
            Event e = AddEvent(makeCurrent: true);
            ReaderPart row = new(new FakeTimingPage(), new TimingSystem("10.0.0.5", Constants.Readers.SYSTEM_RFID),
                [new TimingLocation(TimingConstants.LOCATION_FINISH, e.Identifier, "Finish", 0, 0)]);
            Host(row);
            TextBox port = Find<TextBox>(row, "ReaderPort");
            port.Text = "";

            Type(port, "2");
            Type(port, "x");
            Type(port, "3");

            Assert.Equal("23", port.Text);
        }

        private static async Task Paste(TextBox box, string clipboardText)
        {
            Avalonia.Input.Platform.IClipboard clipboard = TopLevel.GetTopLevel(box)!.Clipboard!;
            await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, clipboardText);
            box.Focus();
            box.Paste();
            await Settle(100);
        }

        [AvaloniaFact]
        public async Task Pasting_into_the_reader_port_keeps_only_digits()
        {
            Event e = AddEvent(makeCurrent: true);
            ReaderPart row = new(new FakeTimingPage(), new TimingSystem("10.0.0.5", Constants.Readers.SYSTEM_RFID),
                [new TimingLocation(TimingConstants.LOCATION_FINISH, e.Identifier, "Finish", 0, 0)]);
            Host(row);
            TextBox port = Find<TextBox>(row, "ReaderPort");
            port.Text = "";

            await Paste(port, "50x8 4");

            Assert.Equal("5084", port.Text);
        }

        [AvaloniaFact]
        public async Task Pasting_into_an_age_group_keeps_only_digits()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance d = AddDistance(e.Identifier);
            AgeGroupPart part = new(null!, new AgeGroup(e.Identifier, d.Identifier, 0, 99));
            Host(part);
            TextBox start = Find<TextBox>(part, "StartAge");
            start.Text = "";

            await Paste(start, "2O9");

            Assert.Equal("29", start.Text);
        }

        [AvaloniaFact]
        public async Task Pasting_into_a_chip_tool_range_keeps_only_digits()
        {
            AddEvent(makeCurrent: true);
            ChipTool tool = Show(ChipTool.NewWindow(MainWindow, Db));
            Click(Find<Button>(tool, "AddRangeButton"));
            TextBox startBib = Find<TextBox>(Find<ListBox>(tool, "CorrelationBox").Items.OfType<TagRangePart>().Last(), "StartBib");
            startBib.Text = "";

            await Paste(startBib, "bib 120");

            Assert.Equal("120", startBib.Text);
        }

        [AvaloniaFact]
        public void Enter_in_the_participant_editor_saves_the_person()
        {
            Event e = AddEvent(makeCurrent: true);
            AddDistance(e.Identifier, "5K");
            ModifyParticipantWindow window = Show(ModifyParticipantWindow.NewWindow(MainWindow, Db));
            Find<TextBox>(window, "BibBox").Text = "42";
            Find<TextBox>(window, "FirstBox").Text = "Ada";
            Find<TextBox>(window, "LastBox").Text = "Lovelace";

            PressKey(Find<TextBox>(window, "LastBox"), PhysicalKey.Enter);

            Participant saved = Assert.Single(Db.GetParticipants(e.Identifier));
            Assert.Equal(("42", "Ada", "Lovelace"), (saved.Bib, saved.FirstName, saved.LastName));
        }

        [AvaloniaFact]
        public void Enter_in_manual_entry_adds_the_read()
        {
            Event e = AddEvent(makeCurrent: true);
            e.StartSeconds = 8 * 3600;
            Db.UpdateEvent(e);
            AddParticipant(e.Identifier, AddDistance(e.Identifier), "200");
            e = Db.GetCurrentEvent()!;
            List<TimingLocation> locations = [new TimingLocation(TimingConstants.LOCATION_FINISH, e.Identifier, "Finish", 1, 0)];
            ManualEntryWindow window = Show(ManualEntryWindow.NewWindow(MainWindow, Db, locations));
            Find<TextBox>(window, "BibBox").Text = "200";
            Find<MaskedTextBox>(window, "TimeBox").Text = "00:45:00.000";
            Find<RadioButton>(window, "NetTimeButton").IsChecked = true;

            PressKey(Find<MaskedTextBox>(window, "TimeBox"), PhysicalKey.Enter);

            ChipRead read = Assert.Single(Db.GetChipReads(e.Identifier));
            Assert.Equal("200", read.Bib);
            Assert.Equal(DateTime.Parse(e.Date).Date.AddHours(8).AddMinutes(45), read.Time);
        }

        [AvaloniaFact]
        public void Enter_in_shift_reads_shifts_them()
        {
            Event e = AddEvent(makeCurrent: true);
            Db.AddChipReads([NewRead(e.Identifier, "1001", 60)]);
            TimingPage timing = new(MainWindow, Db);
            Host(timing);
            ChipRead read = Db.GetChipReads(e.Identifier).Single();
            DateTime before = read.Time;
            EditRawReadsWindow window = Show(new EditRawReadsWindow(timing, Db, [read]));
            Find<MaskedTextBox>(window, "TimeBox").Text = "00:01:30.500";

            PressKey(Find<MaskedTextBox>(window, "TimeBox"), PhysicalKey.Enter);

            Assert.Equal(before.AddSeconds(90.5), Db.GetChipReads(e.Identifier).Single().Time);
            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public void Enter_in_the_new_event_window_creates_the_event()
        {
            NewEventWindow window = Show(NewEventWindow.NewWindow(MainWindow, Db));
            Find<TextBox>(window, "NameBox").Text = "Harvest Half";
            Find<TextBox>(window, "YearCodeBox").Text = "2026";
            Find<CalendarDatePicker>(window, "DatePicker").SelectedDate = new DateTime(2026, 10, 4);

            PressKey(Find<TextBox>(window, "YearCodeBox"), PhysicalKey.Enter);

            Assert.Equal("Harvest Half", Assert.Single(Db.GetEvents()).Name);
            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public void Enter_in_the_start_time_box_sets_the_race_start()
        {
            AddEvent(makeCurrent: true);
            TimingPage page = new(MainWindow, Db);
            Host(page);
            MaskedTextBox startTime = Find<MaskedTextBox>(page, "StartTime");
            startTime.Text = "07:30:15.250";

            PressKey(startTime, PhysicalKey.Enter);

            Event e = Db.GetCurrentEvent()!;
            Assert.Equal(((7 * 3600) + (30 * 60) + 15, 250), (e.StartSeconds, e.StartMilliseconds));
            Assert.False(Find<Button>(page, "StartRace").IsEnabled);
        }

        [AvaloniaFact]
        public void Ctrl_enter_in_the_start_time_box_clears_the_race_start()
        {
            Event e = AddEvent(makeCurrent: true);
            e.StartSeconds = 8 * 3600;
            Db.UpdateEvent(e);
            TimingPage page = new(MainWindow, Db);
            Host(page);
            MaskedTextBox startTime = Find<MaskedTextBox>(page, "StartTime");

            PressKey(startTime, PhysicalKey.Enter, RawInputModifiers.Control);

            Assert.Equal(0, Db.GetCurrentEvent()!.StartSeconds);
            Assert.True(Find<Button>(page, "StartRace").IsEnabled);
            Assert.Equal("00:00:00", Find<TextBlock>(page, "ElapsedTime").Text);
        }

        [AvaloniaFact]
        public void Escape_closes_the_tag_testers_info_window()
        {
            AddEvent(makeCurrent: true);
            ChipReaderWindow tester = Show(ChipReaderWindow.NewWindow(MainWindow, Db));
            Click(Find<Button>(tester, "BeautyBtn"));
            ChipPersonWindow info = tester.PersonWindow!;

            PressKey(info, PhysicalKey.Escape);

            Assert.False(info.IsVisible);
            Assert.Null(tester.PersonWindow);
        }
    }
}
