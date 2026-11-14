using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.Export;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.MainPages.Timing;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// The Timing page's controls around the results grid: who is shown, where, in what order,
    /// the stats condensing, the web server button, the export buttons, and Recalculate.
    /// </summary>
    public class TimingPageControlsTests : UiTestBase
    {
        private Event theEvent = null!;
        private Distance fiveK = null!;
        private Distance tenK = null!;
        private Participant ann = null!, ben = null!, cat = null!;

        private void Seed(int eventType = TimingConstants.EVENT_TYPE_DISTANCE)
        {
            theEvent = AddEvent(makeCurrent: true);
            // Separate start and finish locations, so the location filter has both.
            theEvent.CommonStartFinish = false;
            theEvent.EventType = eventType;
            Db.UpdateEvent(theEvent);
            fiveK = AddDistance(theEvent.Identifier, "5K");
            tenK = AddDistance(theEvent.Identifier, "10K");
            ann = AddParticipant(theEvent.Identifier, fiveK, "10", first: "Ann", last: "Alpha", gender: "F");
            ben = AddParticipant(theEvent.Identifier, fiveK, "9", first: "Ben", last: "Bravo", gender: "M");
            cat = AddParticipant(theEvent.Identifier, tenK, "100", first: "Cat", last: "Charlie", gender: "F");
            theEvent = Db.GetCurrentEvent()!;
        }

        private TimeResult Result(Participant? p, long seconds, int location = TimingConstants.LOCATION_FINISH,
            int segment = TimingConstants.SEGMENT_FINISH, string bib = "") =>
            new(theEvent.Identifier, TimingConstants.TIMERESULT_DUMMYREAD,
                p?.EventSpecific.Identifier ?? TimingConstants.TIMERESULT_DUMMYPERSON,
                location, segment, 1, seconds, 0, p == null ? "chip-" + bib : "", seconds, 0,
                new DateTime(2026, 6, 15, 8, 0, 0).AddSeconds(seconds), p?.Bib ?? bib, TimingConstants.TIMERESULT_STATUS_NONE, "");

        /// <summary>Ann, Ben and Cat finish (Cat first), Ben has a start, and an unknown chip finishes.</summary>
        private void SeedResults()
        {
            Db.AddTimingResults(
            [
                Result(ann, 1500),
                Result(ben, 1800),
                Result(cat, 1200),
                Result(ben, 0, TimingConstants.LOCATION_START, TimingConstants.SEGMENT_START),
                Result(null, 1600, bib: "999"),
            ]);
        }

        private TimingPage OpenPage()
        {
            TimingPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        private static ListBox Grid(TimingPage page) =>
            Find<ListBox>(Assert.IsType<TimingResultsPage>(Find<ContentControl>(page, "TimingFrame").Content), "UpdateListView");

        /// <summary>Each shown result's bib, or "?" for a chip no participant has.</summary>
        private static List<string> Bibs(TimingPage page) =>
            [.. Grid(page).ItemsSource!.Cast<TimeResult>().Select(r => r.EventSpecificId == TimingConstants.TIMERESULT_DUMMYPERSON ? "?" : r.Bib)];

        private static async Task<List<string>> BibsWhen(TimingPage page, Func<List<string>, bool> done, string because)
        {
            await WaitUntil(() => done(Bibs(page)), because: because);
            return Bibs(page);
        }

        private static void Choose(ComboBox box, string text)
        {
            box.SelectedItem = box.Items.Cast<object>().Single(i => (i as ComboBoxItem)?.Content?.ToString() == text || i as string == text);
            Pump();
        }

        // ---- Who is shown ----

        [AvaloniaFact]
        public async Task Unknown_bibs_are_hidden_at_first()
        {
            Seed();
            SeedResults();

            TimingPage page = OpenPage();

            List<string> bibs = await BibsWhen(page, b => b.Count == 4, "the known results to load");
            Assert.DoesNotContain("?", bibs);
        }

        [AvaloniaTheory]
        [InlineData("Show All", new[] { "100", "10", "9", "?", "9" })]
        [InlineData("Show Only Starts", new[] { "9" })]
        [InlineData("Show Only Finishes", new[] { "100", "10", "9", "?" })]
        [InlineData("Show Only Unknown", new[] { "?" })]
        [InlineData("Hide Unknown", new[] { "100", "10", "9", "9" })]
        public async Task People_filter_chooses_which_results_show(string choice, string[] expected)
        {
            Seed();
            SeedResults();
            TimingPage page = OpenPage();
            await BibsWhen(page, b => b.Count == 4, "results to load");

            Choose(Find<ComboBox>(page, "ViewOnlyBox"), choice);

            List<string> want = [.. expected.Order()];
            List<string> bibs = await BibsWhen(page, b => b.Order().SequenceEqual(want), $"'{choice}' to apply");
            Assert.Equal(want, bibs.Order());
        }

        [AvaloniaFact]
        public async Task Typing_a_search_narrows_the_results()
        {
            Seed();
            SeedResults();
            TimingPage page = OpenPage();
            await BibsWhen(page, b => b.Count == 4, "results to load");

            Find<TextBox>(page, "SearchBox").Text = "alpha";

            List<string> bibs = await BibsWhen(page, b => b.Count == 1, "the search to apply");
            Assert.Equal(["10"], bibs);
        }

        [AvaloniaFact]
        public async Task Search_by_bib_matches_the_whole_bib()
        {
            Seed();
            SeedResults();
            TimingPage page = OpenPage();
            await BibsWhen(page, b => b.Count == 4, "results to load");

            Find<TextBox>(page, "SearchBox").Text = " 10 ";

            List<string> bibs = await BibsWhen(page, b => b.Count == 1, "the search to apply");
            Assert.Equal(["10"], bibs);
        }

        // ---- Order ----

        [AvaloniaFact]
        public async Task Sort_by_clock_time_orders_by_race_time()
        {
            Seed();
            SeedResults();
            TimingPage page = OpenPage();
            await BibsWhen(page, b => b.Count == 4, "results to load");

            Choose(Find<ComboBox>(page, "SortBy"), "Clock Time");

            Assert.Equal(["9", "100", "10", "9"], await BibsWhen(page, b => b.SequenceEqual(["9", "100", "10", "9"]), "the clock time sort to apply"));
        }

        [AvaloniaFact]
        public async Task Sort_by_bib_orders_bibs_as_numbers()
        {
            Seed();
            SeedResults();
            TimingPage page = OpenPage();
            await BibsWhen(page, b => b.Count == 4, "results to load");

            Choose(Find<ComboBox>(page, "SortBy"), "Bib");

            List<string> bibs = await BibsWhen(page, b => b[0] == "9" && b[1] == "9", "the bib sort to apply");
            Assert.Equal(["9", "9", "10", "100"], bibs);
        }

        [AvaloniaFact]
        public async Task Sort_by_distance_groups_each_distance()
        {
            Seed();
            SeedResults();
            TimingPage page = OpenPage();
            await BibsWhen(page, b => b.Count == 4, "results to load");

            Choose(Find<ComboBox>(page, "SortBy"), "Distance");

            List<string> bibs = await BibsWhen(page, b => b[0] == "100" || b[^1] == "100", "the distance sort to apply");
            // The 10K runner sits apart from the three 5K results.
            Assert.True(bibs[0] == "100" || bibs[^1] == "100", string.Join(",", bibs));
        }

        // ---- Where ----

        [AvaloniaFact]
        public async Task Location_filter_is_hidden_without_extra_locations()
        {
            Seed();

            TimingPage page = OpenPage();
            await Settle();

            Assert.False(Find<ComboBox>(page, "LocationBox").IsVisible);
        }

        [AvaloniaFact]
        public async Task Location_filter_lists_every_location_but_the_announcer()
        {
            Seed();
            Db.AddTimingLocation(new TimingLocation(theEvent.Identifier, "Mile 3"));

            TimingPage page = OpenPage();
            await Settle();

            ComboBox box = Find<ComboBox>(page, "LocationBox");
            Assert.True(box.IsVisible);
            Assert.Equal(["All Locations", "Start", "Finish", "Mile 3"],
                box.Items.Cast<ComboBoxItem>().Select(i => i.Content!.ToString()!));
        }

        [AvaloniaFact]
        public async Task Location_filter_shows_only_that_locations_results()
        {
            Seed();
            int mile3 = Db.AddTimingLocation(new TimingLocation(theEvent.Identifier, "Mile 3"));
            SeedResults();
            Db.AddTimingResults([Result(ann, 700, mile3, 0)]);
            TimingPage page = OpenPage();
            await BibsWhen(page, b => b.Count == 5, "results to load");

            Choose(Find<ComboBox>(page, "LocationBox"), "Mile 3");
            Assert.Equal(["10"], await BibsWhen(page, b => b.Count == 1, "the Mile 3 filter to apply"));

            Choose(Find<ComboBox>(page, "LocationBox"), "Start");
            Assert.Equal(["9"], await BibsWhen(page, b => b.Count == 1 && b[0] == "9", "the Start filter to apply"));

            Choose(Find<ComboBox>(page, "LocationBox"), "All Locations");
            await BibsWhen(page, b => b.Count == 5, "every location to show again");
        }

        [AvaloniaFact]
        public async Task Common_start_finish_events_filter_on_start_finish()
        {
            Seed();
            theEvent.CommonStartFinish = true;
            Db.UpdateEvent(theEvent);
            Db.AddTimingLocation(new TimingLocation(theEvent.Identifier, "Mile 3"));
            Db.AddTimingResults([Result(ann, 1500), Result(cat, 1200)]);
            TimingPage page = OpenPage();
            await BibsWhen(page, b => b.Count == 2, "results to load");

            ComboBox box = Find<ComboBox>(page, "LocationBox");
            Assert.Equal(["All Locations", "Start/Finish", "Mile 3"], box.Items.Cast<ComboBoxItem>().Select(i => i.Content!.ToString()!));
            Choose(box, "Start/Finish");
            await Settle();

            Assert.Equal(2, Bibs(page).Count);
        }

        [AvaloniaFact]
        public async Task Reader_filter_shows_only_when_readers_are_given()
        {
            Seed();
            TimingPage page = OpenPage();
            await Settle();
            ComboBox readers = Find<ComboBox>(page, "ReaderSelectionBox");
            Assert.False(readers.IsVisible);

            page.SetReaders(["All", "Reader 1", "Reader 2"], true);
            Assert.True(readers.IsVisible);
            Assert.Equal("All", page.GetReader());

            Choose(readers, "Reader 2");
            Assert.Equal("Reader 2", page.GetReader());
        }

        // ---- Stats ----

        [AvaloniaFact]
        public async Task Stats_switch_splits_out_linked_distances()
        {
            Seed();
            Distance halfFiveK = AddDistance(theEvent.Identifier, "5K Walk");
            halfFiveK.LinkedDistance = fiveK.Identifier;
            Db.UpdateDistance(halfFiveK);
            AddParticipant(theEvent.Identifier, halfFiveK, "50");
            TimingPage page = OpenPage();
            await Settle();
            DataGrid stats = Find<DataGrid>(page, "StatsListView");
            List<string> Names() => [.. stats.ItemsSource!.Cast<DistanceStat>().Select(s => s.DistanceName)];

            Assert.DoesNotContain("5K Walk", Names());
            Find<ToggleSwitch>(page, "CondenseSwitch").IsChecked = true;
            Pump();
            Assert.Contains("5K Walk", Names());

            Find<ToggleSwitch>(page, "CondenseSwitch").IsChecked = false;
            Pump();
            Assert.DoesNotContain("5K Walk", Names());
        }

        [AvaloniaFact]
        public async Task Stats_switch_only_shows_while_stats_are_open()
        {
            Seed();
            TimingPage page = OpenPage();
            await Settle();

            Find<Expander>(page, "StatsExpander").IsExpanded = false;
            Pump();
            Assert.False(Find<ToggleSwitch>(page, "CondenseSwitch").IsVisible);

            Find<Expander>(page, "StatsExpander").IsExpanded = true;
            Pump();
            Assert.True(Find<ToggleSwitch>(page, "CondenseSwitch").IsVisible);
        }

        // ---- Web server ----

        [AvaloniaFact]
        public async Task Start_web_starts_the_server_and_shows_its_address()
        {
            Seed();
            TimingPage page = OpenPage();
            await Settle();
            Button web = Find<Button>(page, "HttpServerButton");

            Click(web);

            Assert.Equal(1, MainWindow.StartHttpServerCalls);
            Assert.Equal("Stop Web", ContentText(web));
            Assert.True(Find<StackPanel>(page, "WebPanel").IsVisible);
            Assert.EndsWith(":6933", Find<TextBlock>(page, "WebBlock").Text);
        }

        [AvaloniaFact]
        public async Task Web_address_is_a_link_that_opens_in_a_browser()
        {
            Seed();
            TimingPage page = OpenPage();
            await Settle();

            Uri link = Find<HyperlinkButton>(page, "WebButton").NavigateUri!;
            Assert.Equal("http", link.Scheme);
            Assert.Equal(6933, link.Port);
            Assert.Equal(link.ToString().TrimEnd('/'), Find<TextBlock>(page, "WebBlock").Text);
        }

        [AvaloniaFact]
        public async Task Stop_web_stops_the_server_and_hides_its_address()
        {
            Seed();
            TimingPage page = OpenPage();
            await Settle();
            Button web = Find<Button>(page, "HttpServerButton");
            Click(web);

            Click(web);

            Assert.Equal(1, MainWindow.StopHttpServerCalls);
            Assert.Equal("Start Web", ContentText(web));
            Assert.False(Find<StackPanel>(page, "WebPanel").IsVisible);
        }

        [AvaloniaFact]
        public async Task Web_server_that_fails_to_start_is_stopped_and_left_off()
        {
            Seed();
            MainWindow.HttpServerFails = true;
            TimingPage page = OpenPage();
            await Settle();
            Button web = Find<Button>(page, "HttpServerButton");

            Click(web);

            Assert.Equal(1, MainWindow.StopHttpServerCalls);
            Assert.Equal("Start Web", ContentText(web));
            Assert.False(Find<StackPanel>(page, "WebPanel").IsVisible);
        }

        [AvaloniaFact]
        public async Task A_running_web_server_shows_on_opening_and_updating()
        {
            Seed();
            MainWindow.HttpServerRunning = true;

            TimingPage page = OpenPage();
            await Settle();
            Assert.Equal("Stop Web", ContentText(Find<Button>(page, "HttpServerButton")));

            MainWindow.HttpServerRunning = false;
            page.UpdateView();
            Assert.Equal("Start Web", ContentText(Find<Button>(page, "HttpServerButton")));
            Assert.False(Find<StackPanel>(page, "WebPanel").IsVisible);
        }

        // ---- Exports ----

        [AvaloniaFact]
        public async Task Export_opens_the_results_export()
        {
            Seed();
            ShowMainWindow();
            TimingPage page = OpenPage();
            await Settle();

            Click(Find<Button>(page, "ExportButton"));

            AddedWindow<ExportResults>().Close();
        }

        [AvaloniaTheory]
        [InlineData("BostonExportButton")]
        [InlineData("AbbottExportButton")]
        [InlineData("UltraSignupExportButton")]
        [InlineData("RunSignupExportButton")]
        public async Task Distance_exports_open_for_distance_events(string button)
        {
            Seed();
            ShowMainWindow();
            TimingPage page = OpenPage();
            await Settle();

            Click(Find<Button>(page, button));

            AddedWindow<ExportDistanceResults>().Close();
        }

        [AvaloniaTheory]
        [InlineData("BostonExportButton")]
        [InlineData("AbbottExportButton")]
        [InlineData("UltraSignupExportButton")]
        [InlineData("RunSignupExportButton")]
        public async Task Distance_exports_refuse_time_based_events(string button)
        {
            Seed(TimingConstants.EVENT_TYPE_TIME);
            ShowMainWindow();
            TimingPage page = OpenPage();
            await Settle();

            Click(Find<Button>(page, button));

            Assert.Empty(MainWindow.AddedWindows.OfType<ExportDistanceResults>());
            Assert.Empty(MainWindow.OwnedWindows.OfType<ExportDistanceResults>());
        }

        // ---- Recalculate ----

        [AvaloniaFact]
        public async Task Recalculate_clears_results_and_wakes_the_timing_worker()
        {
            Seed();
            SeedResults();
            TimingPage page = OpenPage();
            await BibsWhen(page, b => b.Count == 4, "results to load");
            Button recalc = Find<Button>(page, "RecalculateButton");

            Click(recalc);
            await WaitUntil(() => MainWindow.NotifyTimingWorkerCalls > 0, because: "the recalculation to finish");

            Assert.Empty(Db.GetTimingResults(theEvent.Identifier));
            Assert.Equal(1, MainWindow.NetworkClearResultsCalls);
            Assert.Equal("Recalculate", ContentText(recalc));
        }

        [AvaloniaFact]
        public async Task Recalculate_can_be_pressed_again_once_finished()
        {
            Seed();
            TimingPage page = OpenPage();
            await Settle();
            Button recalc = Find<Button>(page, "RecalculateButton");

            Click(recalc);
            await WaitUntil(() => MainWindow.NotifyTimingWorkerCalls == 1, because: "the first recalculation");
            Click(recalc);
            await WaitUntil(() => MainWindow.NotifyTimingWorkerCalls == 2, because: "the second recalculation");

            Assert.Equal(2, MainWindow.NetworkClearResultsCalls);
        }

        [AvaloniaFact]
        public async Task Recalculate_ignores_presses_while_working()
        {
            Seed();
            TimingPage page = OpenPage();
            await Settle();
            Button recalc = Find<Button>(page, "RecalculateButton");
            recalc.Content = "Working...";

            Click(recalc);
            await Settle();

            Assert.Equal(0, MainWindow.NotifyTimingWorkerCalls);
            Assert.Equal(0, MainWindow.NetworkClearResultsCalls);
        }
    }
}
