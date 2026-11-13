using System.Net;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.IO;
using Chronokeep.Objects;
using Chronokeep.Test.Network;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.API.Pages;
using Chronokeep.UI.API.Windows;
using Chronokeep.UI.Export;
using ClosedXML.Excel;
using static Chronokeep.Constants.ApiConstants;
using ExportHeaderPart = Chronokeep.UI.Export.Parts.HeaderPart;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// The choose-columns Export Results window, and the Edit Event and Edit Year pages that change the
    /// linked event on the results server.
    /// </summary>
    public class ExportAndEditApiTests : UiTestBase
    {
        private readonly FakeApiServer server = new();

        public override void Dispose()
        {
            base.Dispose();
            server.Dispose();
        }

        private string TempFile(string name) => Path.Combine(Path.GetDirectoryName(DbPath)!, name);

        private ExportResults OpenExport() => Show(new ExportResults(MainWindow, Db));

        private static List<string> Columns(ExportResults window) =>
            [.. Find<ListBox>(window, "HeadersList").Items.OfType<ExportHeaderPart>().Select(h => h.NameValue)];

        /// <summary>A distance event with a finisher, a runner with no results, and a read for a bib nobody registered.</summary>
        private Event SeedFinishers()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            Participant ann = AddParticipant(e.Identifier, fiveK, "1", first: "Ann", last: "Alpha", birthday: "1990/01/02");
            AddParticipant(e.Identifier, fiveK, "2", first: "Ben", last: "Bravo");
            TimeResult Finish(int eventSpecificId, string bib, long seconds, int place) =>
                new(e.Identifier, Constants.Timing.TIMERESULT_DUMMYREAD, eventSpecificId,
                    Constants.Timing.LOCATION_FINISH, Constants.Timing.SEGMENT_FINISH, 1, seconds, 0, "", seconds, 0,
                    DateTime.Now, bib, Constants.Timing.TIMERESULT_STATUS_NONE, "") { Place = place };
            // A result with no participant gets its bib from its read, so bib 99 needs a real (manual) read.
            Db.AddChipReads([new ChipRead(e.Identifier, Constants.Timing.LOCATION_FINISH, "99", DateTime.Now, Constants.Timing.CHIPREAD_STATUS_NONE)]);
            int unknownRead = Db.GetChipReads(e.Identifier).Single(r => r.ReadBib == "99").ReadId;
            TimeResult unknown = new(e.Identifier, unknownRead, Constants.Timing.TIMERESULT_DUMMYPERSON,
                Constants.Timing.LOCATION_FINISH, Constants.Timing.SEGMENT_FINISH, 1, 1700, 0, "", 1700, 0,
                DateTime.Now, "99", Constants.Timing.TIMERESULT_STATUS_NONE, "");
            Db.AddTimingResults([Finish(ann.EventSpecific.Identifier, "1", 1500, 1), unknown]);
            return e;
        }

        [AvaloniaFact]
        public void Distance_events_offer_finish_time_columns()
        {
            SeedFinishers();

            List<string> columns = Columns(OpenExport());

            Assert.Contains("Clock Finish", columns);
            Assert.Contains("Chip Finish", columns);
            Assert.DoesNotContain("Laps Completed", columns);
            Assert.All(Find<ListBox>(OpenExport(), "HeadersList").Items.OfType<ExportHeaderPart>(), h => Assert.True(Find<ToggleSwitch>(h, "Include").IsChecked));
        }

        [AvaloniaFact]
        public void Time_events_offer_lap_columns_instead()
        {
            Event e = AddEvent(makeCurrent: true);
            e.EventType = Constants.Timing.EVENT_TYPE_TIME;
            Db.UpdateEvent(e);

            List<string> columns = Columns(OpenExport());

            Assert.Contains("Laps Completed", columns);
            Assert.DoesNotContain("Chip Finish", columns);
        }

        private static List<Dictionary<string, string>> ReadCsvRows(string path)
        {
            List<string[]> csv = [.. File.ReadAllLines(path).Select(CsvImporter.ParseLine)];
            return [.. csv.Skip(1).Select(r => csv[0].Select((h, i) => (h, v: i < r.Length ? r[i] : "")).ToDictionary(x => x.h, x => x.v))];
        }

        [AvaloniaFact]
        public void Csv_export_writes_only_the_chosen_columns_for_each_participant()
        {
            SeedFinishers();
            string path = TempFile("results.csv");

            OpenExport().WriteResults(["Bib", "First", "Last", "Birthday", "Place", "Chip Finish"], path);

            Assert.Equal(["Bib", "First", "Last", "Birthday", "Place", "Chip Finish"], CsvImporter.ParseLine(File.ReadLines(path).First()));
            List<Dictionary<string, string>> rows = ReadCsvRows(path);
            Dictionary<string, string> ann = rows.Single(r => r["Bib"] == "1");
            Assert.Equal(("Ann", "Alpha", "1990/01/02", "1"), (ann["First"], ann["Last"], ann["Birthday"], ann["Place"]));
            Assert.NotEqual("", ann["Chip Finish"]);
            Dictionary<string, string> ben = rows.Single(r => r["Bib"] == "2");
            Assert.Equal(("Ben", ""), (ben["First"], ben["Place"]));
        }

        [AvaloniaFact]
        public void Export_includes_bibs_with_results_that_nobody_registered()
        {
            SeedFinishers();
            string path = TempFile("results.csv");

            OpenExport().WriteResults(["Bib", "First", "Chip Finish"], path);

            Dictionary<string, string> unknown = Assert.Single(ReadCsvRows(path), r => r["Bib"] == "99");
            Assert.Equal("", unknown["First"]);
            Assert.NotEqual("", unknown["Chip Finish"]);
            Assert.DoesNotContain(ReadCsvRows(path), r => r["Bib"] == "");
        }

        [AvaloniaFact]
        public void Excel_export_is_written_for_an_xlsx_path()
        {
            SeedFinishers();
            string path = TempFile("results.xlsx");

            OpenExport().WriteResults(["Bib", "First"], path);

            using XLWorkbook book = new(path);
            IXLWorksheet sheet = book.Worksheets.First();
            Assert.Equal("Bib", sheet.Cell(1, 1).GetString());
            Assert.Contains(Enumerable.Range(2, 4).Select(r => sheet.Cell(r, 2).GetString()), name => name == "Ann");
        }

        [AvaloniaFact]
        public void Export_cancel_closes_the_window()
        {
            SeedFinishers();
            ExportResults window = OpenExport();

            Click(Find<Button>(window, "Cancel"));

            Assert.False(window.IsVisible);
        }

        private const string EventJson =
            """{"event":{"name":"Spring Fling","cert_name":"Spring Fling 5K","slug":"spring-fling","website":"https://example.com","image":"","contact_email":"race@example.com","access_restricted":false,"type":"distance"}}""";

        private const string YearJson =
            """{"event":{"name":"Spring Fling","slug":"spring-fling","type":"distance"},"event_year":{"year":"2026","date_time":"2026/06/15 08:00:00 -07:00","live":false,"days_allowed":1,"ranking_type":"gun"}}""";

        private EditApiWindow OpenLinkedEventWindow(string eventJson = EventJson, string yearJson = YearJson)
        {
            Event e = AddEvent(makeCurrent: true);
            e.ApiId = Db.AddApi(new ApiObject(0, CHRONOKEEP_RESULTS_SELF, server.Url, "Local", "tok", ""));
            e.ApiEventId = "spring-fling,2026";
            Db.UpdateEvent(e);
            server.Responder = request => request.Path switch
            {
                "event" => (HttpStatusCode.OK, eventJson),
                "event-year" => (HttpStatusCode.OK, yearJson),
                "event/update" => (HttpStatusCode.OK, eventJson),
                "event-year/update" => (HttpStatusCode.OK, yearJson),
                _ => (HttpStatusCode.NotFound, """{"message":"not found"}"""),
            };
            return Show(EditApiWindow.NewWindow(MainWindow, Db));
        }

        private static Control Frame(EditApiWindow window) => (Control)Find<ContentControl>(window, "EditApiFrame").Content!;

        private async Task<EditEventPage> OpenEditEvent(EditApiWindow window)
        {
            Click(Find<Button>(Frame(window), "EditEventButton"));
            EditEventPage page = Assert.IsType<EditEventPage>(Frame(window));
            await WaitUntil(() => Find<Panel>(page, "EventPanel").IsVisible, because: "the event to load");
            return page;
        }

        private async Task<EditYearPage> OpenEditYear(EditApiWindow window)
        {
            Click(Find<Button>(Frame(window), "EditYearButton"));
            EditYearPage page = Assert.IsType<EditYearPage>(Frame(window));
            await WaitUntil(() => Find<Panel>(page, "YearPanel").IsVisible, because: "the year to load");
            return page;
        }

        private JsonElement BodyOf(string path) => JsonDocument.Parse(server.Requests.Single(r => r.Path == path).Body).RootElement;

        [AvaloniaFact]
        public async Task Edit_event_shows_the_servers_values_with_the_slug_locked()
        {
            EditEventPage page = await OpenEditEvent(OpenLinkedEventWindow());

            Assert.Equal("Spring Fling", Find<TextBox>(page, "NameBox").Text);
            Assert.Equal("race@example.com", Find<TextBox>(page, "ContactBox").Text);
            Assert.Equal("spring-fling", Find<TextBox>(page, "SlugBox").Text);
            Assert.False(Find<TextBox>(page, "SlugBox").IsEnabled);
            Assert.Equal("Distance", ((ComboBoxItem)Find<ComboBox>(page, "TypeBox").SelectedItem!).Content);
            Assert.True(Find<Button>(page, "SaveButton").IsEnabled);
        }

        [AvaloniaFact]
        public async Task Edit_event_save_sends_the_changes_and_closes()
        {
            EditApiWindow window = OpenLinkedEventWindow();
            EditEventPage page = await OpenEditEvent(window);
            Find<TextBox>(page, "NameBox").Text = "Spring Fling Classic";
            ComboBox types = Find<ComboBox>(page, "TypeBox");
            types.SelectedItem = types.Items.OfType<ComboBoxItem>().Single(i => (string)i.Content! == "Time");
            Find<ToggleSwitch>(page, "RestrictBox").IsChecked = true;

            Click(Find<Button>(page, "SaveButton"));
            await WaitUntil(() => !window.IsVisible, because: "the window to close");

            JsonElement sent = BodyOf("event/update").GetProperty("event");
            Assert.Equal("Spring Fling Classic", sent.GetProperty("name").GetString());
            Assert.Equal("spring-fling", sent.GetProperty("slug").GetString());
            Assert.Equal(CHRONOKEEP_EVENT_TYPE_TIME, sent.GetProperty("type").GetString());
            Assert.True(sent.GetProperty("access_restricted").GetBoolean());
        }

        [AvaloniaFact]
        public async Task Edit_event_cancel_sends_nothing()
        {
            EditApiWindow window = OpenLinkedEventWindow();
            EditEventPage page = await OpenEditEvent(window);

            Click(Find<Button>(page, "CancelButton"));

            Assert.False(window.IsVisible);
            Assert.DoesNotContain(server.Requests, r => r.Path == "event/update");
        }

        [AvaloniaFact]
        public async Task Edit_event_closes_when_the_server_refuses()
        {
            EditApiWindow window = OpenLinkedEventWindow();
            server.Responder = _ => (HttpStatusCode.Unauthorized, """{"message":"bad token"}""");

            Click(Find<Button>(Frame(window), "EditEventButton"));
            await WaitUntil(() => !window.IsVisible, because: "the window to close on error");
        }

        [AvaloniaFact]
        public async Task Edit_year_shows_the_servers_values_with_the_year_locked()
        {
            EditYearPage page = await OpenEditYear(OpenLinkedEventWindow());

            Assert.Equal("2026", Find<TextBox>(page, "YearBox").Text);
            Assert.False(Find<TextBox>(page, "YearBox").IsEnabled);
            Assert.Equal(new DateTime(2026, 6, 15).Date, Find<CalendarDatePicker>(page, "DateBox").SelectedDate!.Value.Date);
            Assert.Equal(["Clock", "Chip"], Find<ComboBox>(page, "RankBox").Items.OfType<ComboBoxItem>().Select(i => (string)i.Content!));
            Assert.Equal("1", Find<TextBlock>(page, "DaysAllowedText").Text);
        }

        [AvaloniaFact]
        public async Task Edit_year_save_sends_the_changes_for_the_same_year()
        {
            EditApiWindow window = OpenLinkedEventWindow();
            EditYearPage page = await OpenEditYear(window);
            Find<ToggleSwitch>(page, "LiveBox").IsChecked = true;
            Find<ComboBox>(page, "RankBox").SelectedIndex = 1;
            Find<Slider>(page, "DaysAllowedSlider").Value = 3;

            Click(Find<Button>(page, "SaveButton"));
            await WaitUntil(() => !window.IsVisible, because: "the window to close");

            JsonElement body = BodyOf("event-year/update");
            Assert.Equal("spring-fling", body.GetProperty("slug").GetString());
            JsonElement year = body.GetProperty("event_year");
            Assert.Equal("2026", year.GetProperty("year").GetString());
            Assert.True(year.GetProperty("live").GetBoolean());
            Assert.Equal(3, year.GetProperty("days_allowed").GetInt32());
            Assert.Equal("chip", year.GetProperty("ranking_type").GetString());
        }

        [AvaloniaFact]
        public async Task Edit_year_for_a_backyard_ultra_ranks_by_elapsed_or_cumulative()
        {
            string backyardYear = YearJson.Replace("\"type\":\"distance\"", "\"type\":\"backyardultra\"");
            EditYearPage page = await OpenEditYear(OpenLinkedEventWindow(yearJson: backyardYear));

            Assert.Equal(["Elapsed", "Cumulative"], Find<ComboBox>(page, "RankBox").Items.OfType<ComboBoxItem>().Select(i => (string)i.Content!));
        }
    }
}
