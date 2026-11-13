using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.IO;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.ChipAssignment;
using Chronokeep.UI.Import;
using Chronokeep.UI.Parts;
using Chronokeep.UI.Timing.Import;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// The participant import, bib/chip import, and timing log import windows, driven with small CSV files.
    /// </summary>
    public class ImportWindowTests : UiTestBase
    {
        private string WriteCsv(string name, params string[] lines)
        {
            string path = Path.Combine(Path.GetDirectoryName(DbPath)!, name);
            File.WriteAllLines(path, lines);
            return path;
        }

        private ImportFileWindow OpenParticipantImport(string path)
        {
            CsvImporter importer = new(path);
            importer.FetchHeaders();
            return Show(ImportFileWindow.NewWindow(MainWindow, importer, Db));
        }

        private static Control Page(ImportFileWindow window) => (Control)Find<ContentControl>(window, "Frame").Content!;

        private static List<HeaderPart> HeaderRows(ImportFileWindow window) =>
            [.. Find<ListBox>(Page(window), "HeaderListBox").Items.OfType<HeaderPart>()];

        private static List<DistanceAlternatePart> DistanceRows(ImportFileWindow window) =>
            [.. Find<ListBox>(Page(window), "DistanceListBox").Items.OfType<DistanceAlternatePart>()];

        private static void MapDistance(DistanceAlternatePart row, Distance distance)
        {
            ComboBox box = Find<ComboBox>(row, "Distances");
            box.SelectedItem = box.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag! == distance.Identifier.ToString());
        }

        private async Task FinishImport(ImportFileWindow window)
        {
            Click(Find<Button>(window, "Done"));
            Assert.IsType<ImportFilePage2Alt>(Page(window));
            Click(Find<Button>(window, "Done"));
            await WaitUntil(() => !window.IsVisible, because: "the import to finish");
        }

        [AvaloniaFact]
        public async Task Importing_participants_maps_headers_creates_new_distances_and_normalizes_birthdates()
        {
            Event e = AddEvent(makeCurrent: true);
            AddDistance(e.Identifier, "5K");
            string path = WriteCsv("people.csv",
                "First,Last,Birthday,Bib,Distance",
                "Ann,Alpha,1990-01-02,1,5K",
                "Ben,Bravo,3/4/1985,2,Half Marathon");
            ImportFileWindow window = OpenParticipantImport(path);
            Assert.Equal(ImportFileWindow.FIRST, Find<ComboBox>(HeaderRows(window)[0], "HeaderBox").SelectedIndex);

            await FinishImport(window);

            Dictionary<string, Participant> people = Db.GetParticipants(e.Identifier).ToDictionary(p => p.FirstName);
            Assert.Equal(("1", "5K", "1990/01/02"), (people["Ann"].Bib, people["Ann"].Distance, people["Ann"].Birthdate));
            Assert.Equal(("2", "Half Marathon"), (people["Ben"].Bib, people["Ben"].Distance));
            Assert.Equal(["5K", "Half Marathon"], Db.GetDistances(e.Identifier).Select(d => d.Name).Order());
            Assert.Equal(1, MainWindow.NetworkClearResultsCalls);
        }

        [AvaloniaFact]
        public async Task A_file_distance_can_be_mapped_to_an_existing_distance()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance half = AddDistance(e.Identifier, "Half");
            string path = WriteCsv("people.csv", "First,Last,Distance", "Ann,Alpha,13.1 Miles");
            ImportFileWindow window = OpenParticipantImport(path);
            Click(Find<Button>(window, "Done"));

            MapDistance(Assert.Single(DistanceRows(window)), half);
            Click(Find<Button>(window, "Done"));
            await WaitUntil(() => !window.IsVisible, because: "the import to finish");

            Assert.Equal("Half", Assert.Single(Db.GetParticipants(e.Identifier)).Distance);
            Assert.Single(Db.GetDistances(e.Identifier));
        }

        [AvaloniaFact]
        public async Task Without_a_distance_column_everyone_goes_to_the_chosen_distance()
        {
            Event e = AddEvent(makeCurrent: true);
            AddDistance(e.Identifier, "5K");
            Distance tenK = AddDistance(e.Identifier, "10K");
            string path = WriteCsv("people.csv", "First,Last", "Ann,Alpha", "Ben,Bravo");
            ImportFileWindow window = OpenParticipantImport(path);
            Click(Find<Button>(window, "Done"));

            MapDistance(Assert.Single(DistanceRows(window)), tenK);
            Click(Find<Button>(window, "Done"));
            await WaitUntil(() => !window.IsVisible, because: "the import to finish");

            Assert.All(Db.GetParticipants(e.Identifier), p => Assert.Equal("10K", p.Distance));
            Assert.Equal(2, Db.GetDistances(e.Identifier).Count);
        }

        [AvaloniaFact]
        public async Task Without_a_distance_column_auto_uses_the_first_distance_and_adds_none()
        {
            Event e = AddEvent(makeCurrent: true);
            AddDistance(e.Identifier, "5K");
            string path = WriteCsv("people.csv", "First,Last", "Ann,Alpha");
            ImportFileWindow window = OpenParticipantImport(path);

            await FinishImport(window);

            Assert.Equal("5K", Assert.Single(Db.GetParticipants(e.Identifier)).Distance);
            Assert.Equal("5K", Assert.Single(Db.GetDistances(e.Identifier)).Name);
        }

        [AvaloniaFact]
        public async Task Importing_a_bib_for_someone_already_entered_without_one_sets_their_bib()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            AddParticipant(e.Identifier, fiveK, "", first: "Jane", last: "Runner", birthday: "1990/01/02");
            string path = WriteCsv("people.csv",
                "First,Last,Street,Zip,Birthday,Bib,Distance",
                "Jane,Runner,123 Main St,97477,1990-01-02,77,5K");
            ImportFileWindow window = OpenParticipantImport(path);

            await FinishImport(window);

            Assert.Equal("77", Assert.Single(Db.GetParticipants(e.Identifier)).Bib);
        }

        [AvaloniaFact]
        public void A_file_without_a_name_column_is_not_imported()
        {
            Event e = AddEvent(makeCurrent: true);
            AddDistance(e.Identifier);
            string path = WriteCsv("people.csv", "Bib,Distance", "1,5K");
            ImportFileWindow window = OpenParticipantImport(path);

            Click(Find<Button>(window, "Done"));

            Assert.IsType<ImportFilePage1>(Page(window));
            Assert.Empty(Db.GetParticipants(e.Identifier));
        }

        [AvaloniaFact]
        public void Cancel_closes_the_participant_import_without_importing()
        {
            Event e = AddEvent(makeCurrent: true);
            AddDistance(e.Identifier);
            ImportFileWindow window = OpenParticipantImport(WriteCsv("people.csv", "First,Last", "Ann,Alpha"));

            Click(Find<Button>(window, "Cancel"));

            Assert.False(window.IsVisible);
            Assert.Empty(Db.GetParticipants(e.Identifier));
        }

        private BibChipAssociationWindow OpenBibChipImport(string path)
        {
            CsvImporter importer = new(path);
            importer.FetchHeaders();
            return Show(BibChipAssociationWindow.NewWindow(MainWindow, importer, Db));
        }

        private List<(string Bib, string Chip)> Chips(Event e) =>
            [.. Db.GetBibChips(e.Identifier).Select(b => (b.Bib, b.Chip)).OrderBy(b => b.Chip)];

        [AvaloniaFact]
        public async Task Bib_chip_import_adds_each_row_and_skips_blank_ones()
        {
            Event e = AddEvent(makeCurrent: true);
            BibChipAssociationWindow window = OpenBibChipImport(WriteCsv("chips.csv", "Bib,Chip", "1,1001", "2,1002", ",1003", "4,"));

            Click(Find<Button>(window, "Done"));
            await WaitUntil(() => !window.IsVisible, because: "the import to finish");

            Assert.Equal([("1", "1001"), ("2", "1002")], Chips(e));
            Assert.True(window.ImportComplete);
        }

        [AvaloniaFact]
        public async Task Bib_chip_import_asks_before_replacing_a_chip_linked_to_another_bib()
        {
            Event e = AddEvent(makeCurrent: true);
            Db.AddBibChipAssociation(e.Identifier, [new BibChipAssociation { EventId = e.Identifier, Bib = "9", Chip = "1001" }]);
            BibChipAssociationWindow window = OpenBibChipImport(WriteCsv("chips.csv", "Bib,Chip", "1,1001", "2,1002"));

            // The confirmation needs the real main window, so it is never answered here.
            Click(Find<Button>(window, "Done"));
            await WaitUntil(() => !window.IsVisible, because: "the import to finish");

            Assert.Equal([("9", "1001"), ("2", "1002")], Chips(e));
        }

        [AvaloniaFact]
        public async Task Bib_chip_import_without_a_header_row_imports_the_first_line()
        {
            Event e = AddEvent(makeCurrent: true);
            BibChipAssociationWindow window = OpenBibChipImport(WriteCsv("chips.csv", "1,1001", "2,1002"));
            List<BibChipHeaderPart> rows = [.. Find<ListBox>(window, "HeaderListBox").Items.OfType<BibChipHeaderPart>()];
            Find<ComboBox>(rows[0], "HeaderBox").SelectedIndex = 1;
            Find<ComboBox>(rows[1], "HeaderBox").SelectedIndex = 2;
            Find<ToggleSwitch>(window, "Headers").IsChecked = false;

            Click(Find<Button>(window, "Done"));
            await WaitUntil(() => !window.IsVisible, because: "the import to finish");

            Assert.Equal([("1", "1001"), ("2", "1002")], Chips(e));
        }

        [AvaloniaFact]
        public void Bib_chip_import_needs_both_columns()
        {
            Event e = AddEvent(makeCurrent: true);
            BibChipAssociationWindow window = OpenBibChipImport(WriteCsv("chips.csv", "Bib,Tag", "1,1001"));

            Click(Find<Button>(window, "Done"));
            Pump();

            Assert.True(window.IsVisible);
            Assert.Empty(Chips(e));
        }

        private ImportLogWindow OpenLogImport(string path)
        {
            LogImporter importer = new(path);
            importer.FindType();
            return Show(ImportLogWindow.NewWindow(MainWindow, importer, Db));
        }

        private static Control LogPage(ImportLogWindow window) => (Control)Find<UserControl>(window, "Frame").Content!;

        [AvaloniaFact]
        public async Task Rfid_logs_are_recognized_and_imported_to_the_chosen_location()
        {
            Event e = AddEvent(makeCurrent: true);
            int mile = Db.AddTimingLocation(new TimingLocation(e.Identifier, "Mile 6"));
            ImportLogWindow window = OpenLogImport(WriteCsv("rfid.txt",
                "0,1001,1,\"08:00:01.000\"",
                "0,1002,1,\"08:00:02.500\""));
            Control page1 = LogPage(window);
            Assert.Equal("RFID", ((ComboBoxItem)Find<ComboBox>(page1, "TypeHolder").SelectedItem!).Content);
            ComboBox locations = Find<ComboBox>(page1, "LocationHolder");
            locations.SelectedItem = locations.Items.OfType<ComboBoxItem>().Single(i => (string)i.Content! == "Mile 6");

            Click(Find<Button>(page1, "NextButton"));
            await WaitUntil(() => !window.IsVisible, because: "the import to finish");

            List<ChipRead> reads = Db.GetChipReads(e.Identifier);
            Assert.Equal(["1001", "1002"], reads.Select(r => r.ChipNumber).Order());
            Assert.All(reads, r => Assert.Equal(mile, r.LocationId));
            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public async Task Custom_logs_import_the_chosen_columns_and_skip_a_header_line()
        {
            Event e = AddEvent(makeCurrent: true);
            DateTime raceDay = DateTime.Parse(e.Date).Date;
            ImportLogWindow window = OpenLogImport(WriteCsv("custom.csv",
                "tag,when",
                "1001,08:00:01.000",
                $"1002,{raceDay:yyyy-MM-dd} 08:00:02.500"));
            Control page1 = LogPage(window);
            Assert.Equal("CUSTOM", ((ComboBoxItem)Find<ComboBox>(page1, "TypeHolder").SelectedItem!).Content);

            Click(Find<Button>(page1, "NextButton"));
            Control page2 = LogPage(window);
            List<LogPart> columns = [.. Find<ListBox>(page2, "ItemListBox").Items.OfType<LogPart>()];
            Find<ComboBox>(columns[0], "HeaderBox").SelectedIndex = 1;
            Find<ComboBox>(columns[1], "HeaderBox").SelectedIndex = 2;
            Click(Find<Button>(page2, "ImportButton"));
            await WaitUntil(() => !window.IsVisible, because: "the import to finish");

            Dictionary<string, DateTime> reads = Db.GetChipReads(e.Identifier).ToDictionary(r => r.ChipNumber, r => r.Time);
            Assert.Equal(["1001", "1002"], reads.Keys.Order());
            Assert.Equal(raceDay.AddHours(8).AddSeconds(1), reads["1001"]);
            Assert.Equal(raceDay.AddHours(8).AddSeconds(2.5), reads["1002"]);
        }

        [AvaloniaFact]
        public void Custom_logs_need_both_a_chip_and_a_time_column()
        {
            Event e = AddEvent(makeCurrent: true);
            ImportLogWindow window = OpenLogImport(WriteCsv("custom.csv", "tag,when", "1001,08:00:01.000"));
            Click(Find<Button>(LogPage(window), "NextButton"));
            Control page2 = LogPage(window);
            Find<ComboBox>(Find<ListBox>(page2, "ItemListBox").Items.OfType<LogPart>().First(), "HeaderBox").SelectedIndex = 1;

            Click(Find<Button>(page2, "ImportButton"));
            Pump();

            Assert.True(window.IsVisible);
            Assert.Empty(Db.GetChipReads(e.Identifier));
        }
    }
}
