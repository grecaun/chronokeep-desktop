using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.IO;
using Chronokeep.Objects;
using Chronokeep.Objects.Changelog;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.ChipAssignment;
using Chronokeep.UI.ChipAssignment.Parts;
using Chronokeep.UI.Import;
using Chronokeep.UI.Parts;
using Chronokeep.UI.Timing;
using Chronokeep.UI.Timing.Import;
using Chronokeep.UI.Timing.Notifications;
using Chronokeep.UI.Util;
using ClosedXML.Excel;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// Excel participant imports, Chronokeep and IPICO timing logs, the Chip Tool, SMS alerts by wave,
    /// and the changelog window.
    /// </summary>
    public class MoreImportsAndToolsTests : UiTestBase
    {
        private string TempFile(string name) => Path.Combine(Path.GetDirectoryName(DbPath)!, name);

        private string WriteWorkbook()
        {
            string path = TempFile("people.xlsx");
            using XLWorkbook book = new();
            IXLWorksheet info = book.AddWorksheet("Info");
            info.Cell(1, 1).Value = "Race notes";
            IXLWorksheet runners = book.AddWorksheet("Runners");
            string[] headers = ["First", "Last", "Birthday", "Bib", "Distance"];
            for (int i = 0; i < headers.Length; i++)
            {
                runners.Cell(1, i + 1).Value = headers[i];
            }
            runners.Cell(2, 1).Value = "Ann";
            runners.Cell(2, 2).Value = "Alpha";
            runners.Cell(2, 3).Value = new DateTime(1990, 1, 2);
            runners.Cell(2, 4).Value = 1;
            runners.Cell(2, 5).Value = "5K";
            // Row 3 is left blank on purpose.
            runners.Cell(4, 1).Value = "Ben";
            runners.Cell(4, 2).Value = "Bravo";
            runners.Cell(4, 4).Value = 2;
            runners.Cell(4, 5).Value = "5K";
            book.SaveAs(path);
            return path;
        }

        [AvaloniaFact]
        public async Task Excel_imports_read_the_chosen_sheet_skip_blank_rows_and_store_dates()
        {
            Event e = AddEvent(makeCurrent: true);
            AddDistance(e.Identifier, "5K");
            ExcelImporter importer = new(WriteWorkbook());
            importer.FetchHeaders();
            ImportFileWindow window = Show(ImportFileWindow.NewWindow(MainWindow, importer, Db));
            ComboBox sheets = Find<ComboBox>(window, "SheetsBox");
            Assert.True(sheets.IsVisible);
            Assert.Equal(["Info", "Runners"], sheets.ItemsSource!.Cast<string>());

            sheets.SelectedIndex = 1;
            Pump();
            Control page = (Control)Find<ContentControl>(window, "Frame").Content!;
            List<HeaderPart> headers = [.. Find<ListBox>(page, "HeaderListBox").Items.OfType<HeaderPart>()];
            Assert.Equal("First", Find<TextBlock>(headers[0], "HeaderLabel").Text);

            Click(Find<Button>(window, "Done"));
            Click(Find<Button>(window, "Done"));
            await WaitUntil(() => !window.IsVisible, because: "the import to finish");

            Dictionary<string, Participant> people = Db.GetParticipants(e.Identifier).ToDictionary(p => p.FirstName);
            Assert.Equal(["Ann", "Ben"], people.Keys.Order());
            Assert.Equal(("1", "1990/01/02"), (people["Ann"].Bib, people["Ann"].Birthdate));
        }

        private ImportLogWindow OpenLog(string path)
        {
            LogImporter importer = new(path);
            importer.FindType();
            return Show(ImportLogWindow.NewWindow(MainWindow, importer, Db));
        }

        private static Control LogPage(ImportLogWindow window) => (Control)Find<UserControl>(window, "Frame").Content!;

        [AvaloniaFact]
        public async Task Ipico_logs_are_recognized_and_read()
        {
            Event e = AddEvent(makeCurrent: true);
            // aa, reader 01, chip 000000001234, 4 filler, yyMMddHHmmss, hundredths in hex (0x32 = 50 = 500 ms), check.
            string line = "aa" + "01" + "000000001234" + "0000" + "260615080001" + "32" + "00";
            string path = TempFile("ipico.log");
            File.WriteAllLines(path, [line]);
            ImportLogWindow window = OpenLog(path);
            Assert.Equal("IPICO", ((ComboBoxItem)Find<ComboBox>(LogPage(window), "TypeHolder").SelectedItem!).Content);

            Click(Find<Button>(LogPage(window), "NextButton"));
            await WaitUntil(() => !window.IsVisible, because: "the import to finish");

            ChipRead read = Assert.Single(Db.GetChipReads(e.Identifier));
            Assert.Equal("000000001234", read.ChipNumber);
            Assert.Equal(new DateTime(2026, 6, 15, 8, 0, 1, 500), read.Time);
        }

        [AvaloniaFact]
        public async Task Chronokeep_logs_are_recognized_and_read()
        {
            Event e = AddEvent(makeCurrent: true);
            ChipRead original = NewRead(e.Identifier, "1001", 75);
            string path = TempFile("chronokeep.csv");
            File.WriteAllLines(path, [
                "status,chip_number,seconds,milliseconds,time_seconds,time_milliseconds,antenna,reader,box,log_index,rssi,is_rewind,reader_time,start_time,read_bib,type",
                $"0,1001,{original.Seconds},{original.Milliseconds},{original.TimeSeconds},{original.TimeMilliseconds},1,1,Box A,7,-50,0,,0,,0",
            ]);
            ImportLogWindow window = OpenLog(path);
            Assert.Equal("CHRONOKEEP", ((ComboBoxItem)Find<ComboBox>(LogPage(window), "TypeHolder").SelectedItem!).Content);

            Click(Find<Button>(LogPage(window), "NextButton"));
            await WaitUntil(() => !window.IsVisible, because: "the import to finish");

            ChipRead read = Assert.Single(Db.GetChipReads(e.Identifier));
            Assert.Equal(("1001", "Box A"), (read.ChipNumber, read.Box));
            Assert.Equal(original.Time, read.Time);
        }

        private ChipTool OpenChipTool() => Show(ChipTool.NewWindow(MainWindow, Db));

        private static List<TagRangePart> Ranges(ChipTool tool) =>
            [.. Find<ListBox>(tool, "CorrelationBox").Items.OfType<TagRangePart>()];

        private static void SetRange(TagRangePart range, int startBib, int endBib, int startChip)
        {
            Find<TextBox>(range, "StartBib").Text = startBib.ToString();
            Find<TextBox>(range, "EndBib").Text = endBib.ToString();
            Find<TextBox>(range, "StartChip").Text = startChip.ToString();
            Pump();
        }

        private List<(string Bib, string Chip)> Chips(Event e) =>
            [.. Db.GetBibChips(e.Identifier).Select(b => (b.Bib, b.Chip)).OrderBy(b => int.Parse(b.Bib))];

        [AvaloniaFact]
        public void Chip_tool_links_a_range_of_bibs_to_consecutive_chips()
        {
            Event e = AddEvent(makeCurrent: true);
            ChipTool tool = OpenChipTool();
            TagRangePart range = Assert.Single(Ranges(tool));

            SetRange(range, 1, 3, 1001);
            Assert.Equal("1003", Find<TextBlock>(range, "EndChip").Text);
            Click(Find<Button>(tool, "SaveButton"));

            Assert.Equal([("1", "1001"), ("2", "1002"), ("3", "1003")], Chips(e));
            Assert.True(tool.ImportComplete);
            Assert.False(tool.IsVisible);
        }

        [AvaloniaFact]
        public void Chip_tool_new_ranges_continue_after_the_last_one()
        {
            Event e = AddEvent(makeCurrent: true);
            ChipTool tool = OpenChipTool();
            SetRange(Ranges(tool)[0], 1, 3, 1001);

            Click(Find<Button>(tool, "AddRangeButton"));
            TagRangePart next = Ranges(tool)[1];
            Assert.Equal(("4", "1004"), (Find<TextBox>(next, "StartBib").Text, Find<TextBox>(next, "StartChip").Text));
            Find<TextBox>(next, "EndBib").Text = "5";
            Pump();
            Click(Find<Button>(tool, "SaveButton"));

            Assert.Equal(5, Chips(e).Count);
            Assert.Equal(("5", "1005"), Chips(e)[^1]);
        }

        [AvaloniaFact]
        public void Chip_tool_refuses_overlapping_or_backwards_ranges()
        {
            Event e = AddEvent(makeCurrent: true);
            ChipTool tool = OpenChipTool();
            SetRange(Ranges(tool)[0], 1, 10, 1001);
            Click(Find<Button>(tool, "AddRangeButton"));
            SetRange(Ranges(tool)[1], 5, 15, 2001);

            Click(Find<Button>(tool, "SaveButton"));
            Assert.Empty(Chips(e));
            Assert.True(tool.IsVisible);

            Click(Find<Button>(Ranges(tool)[1], "Remove"));
            SetRange(Ranges(tool)[0], 10, 1, 1001);
            Click(Find<Button>(tool, "SaveButton"));
            Assert.Empty(Chips(e));
        }

        [AvaloniaFact]
        public void Chip_tool_cancel_saves_nothing()
        {
            Event e = AddEvent(makeCurrent: true);
            ChipTool tool = OpenChipTool();
            SetRange(Ranges(tool)[0], 1, 3, 1001);

            Click(Find<Button>(tool, "CancelButton"));

            Assert.Empty(Chips(e));
            Assert.False(tool.ImportComplete);
        }

        private Event SeedWaves()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            Distance tenK = AddDistance(e.Identifier, "10K");
            Distance half = AddDistance(e.Identifier, "Half");
            fiveK.Wave = 1;
            tenK.Wave = 2;
            half.Wave = 2;
            Db.UpdateDistance(fiveK);
            Db.UpdateDistance(tenK);
            Db.UpdateDistance(half);
            return e;
        }

        private static List<WaveSms> Waves(SmsWaveEnabledWindow window) =>
            [.. Find<ListBox>(window, "WaveList").ItemsSource!.Cast<WaveSms>()];

        [AvaloniaFact]
        public void Sms_alerts_turn_on_for_every_distance_in_a_wave()
        {
            Event e = SeedWaves();
            SmsWaveEnabledWindow window = Show(new SmsWaveEnabledWindow(MainWindow, Db));
            Assert.Equal([1, 2], Waves(window).Select(w => w.Wave));
            Assert.All(Waves(window), w => Assert.False(w.SmsEnabled));

            Waves(window).Single(w => w.Wave == 2).SmsEnabled = true;
            Click(Find<Button>(window, "Set"));

            Dictionary<string, bool> enabled = Db.GetDistances(e.Identifier).ToDictionary(d => d.Name, d => d.SmsEnabled);
            Assert.Equal((false, true, true), (enabled["5K"], enabled["10K"], enabled["Half"]));
            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);
            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public void Sms_alerts_done_changes_nothing()
        {
            Event e = SeedWaves();
            SmsWaveEnabledWindow window = Show(new SmsWaveEnabledWindow(MainWindow, Db));
            Waves(window)[0].SmsEnabled = true;

            Click(Find<Button>(window, "Done"));

            Assert.All(Db.GetDistances(e.Identifier), d => Assert.False(d.SmsEnabled));
            Assert.Equal(0, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public void Changelog_shows_the_five_newest_versions_with_the_newest_open()
        {
            Db.SetAppSetting(Constants.Settings.AUTO_SHOW_CHANGELOG, Constants.Settings.SETTING_TRUE);
            ChangeLogWindow window = Show(ChangeLogWindow.NewWindow(MainWindow, Db));

            List<Entry> entries = [.. Find<ItemsControl>(window, "LogList").ItemsSource!.Cast<Entry>()];
            Assert.Equal(5, entries.Count);
            Assert.Equal("v2.0.0", entries[0].Version);
            Assert.True(entries[0].IsExpanded);
            Assert.All(entries.Skip(1), en => Assert.False(en.IsExpanded));
            Assert.True(Find<ToggleSwitch>(window, "AutoChangelogToggleSwitch").IsChecked);
        }

        [AvaloniaFact]
        public void Changelog_remembers_whether_to_show_itself_automatically()
        {
            Db.SetAppSetting(Constants.Settings.AUTO_SHOW_CHANGELOG, Constants.Settings.SETTING_TRUE);
            ChangeLogWindow window = Show(ChangeLogWindow.NewWindow(MainWindow, Db));

            Find<ToggleSwitch>(window, "AutoChangelogToggleSwitch").IsChecked = false;
            Click(Find<Button>(window, "CancelButton"));

            Assert.Equal(Constants.Settings.SETTING_FALSE, Db.GetAppSetting(Constants.Settings.AUTO_SHOW_CHANGELOG)!.Value);
        }
    }
}
