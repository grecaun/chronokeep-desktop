using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.MainPages.Timing;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// The Timing page's per-distance participant counts and its results grid. Runsignup excludes
    /// removed participants from these counts and tints their result rows, so both are pinned here.
    /// </summary>
    public class TimingPageTests : UiTestBase
    {
        private (Event Event, Participant Ann, Participant Cat) Seed()
        {
            Event theEvent = AddEvent(makeCurrent: true);
            Distance fiveK = AddDistance(theEvent.Identifier, "5K");
            Distance tenK = AddDistance(theEvent.Identifier, "10K");
            Participant ann = AddParticipant(theEvent.Identifier, fiveK, "1", first: "Ann", last: "Alpha");
            AddParticipant(theEvent.Identifier, fiveK, "2", first: "Ben", last: "Bravo");
            Participant cat = AddParticipant(theEvent.Identifier, tenK, "3", first: "Cat", last: "Charlie");
            return (Db.GetCurrentEvent()!, ann, cat);
        }

        private TimingPage OpenPage()
        {
            TimingPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        private static List<DistanceStat> Stats(TimingPage page) =>
            [.. Find<DataGrid>(page, "StatsListView").ItemsSource!.Cast<DistanceStat>()];

        [AvaloniaFact]
        public async Task Distance_counts_total_every_participant_with_an_all_row()
        {
            Seed();

            TimingPage page = OpenPage();
            await Settle();
            Dictionary<string, DistanceStat> stats = Stats(page).ToDictionary(s => s.DistanceName);

            Assert.Equal(3, stats["All"].Total);
            Assert.Equal(2, stats["5K"].Total);
            Assert.Equal(1, stats["10K"].Total);
        }

        [AvaloniaFact]
        public async Task Distance_counts_follow_participant_status()
        {
            (Event theEvent, Participant ann, Participant cat) = Seed();
            ann.Status = Constants.Timing.EVENTSPECIFIC_FINISHED;
            cat.Status = Constants.Timing.EVENTSPECIFIC_STARTED;
            Db.UpdateParticipants([ann, cat]);

            TimingPage page = OpenPage();
            await Settle();
            Dictionary<string, DistanceStat> stats = Stats(page).ToDictionary(s => s.DistanceName);

            Assert.Equal(1, stats["5K"].Finished);
            Assert.Equal(1, stats["10K"].Active);
            Assert.Equal(1, stats["All"].Finished);
            Assert.Equal(1, stats["All"].Active);
        }

        private TimeResult FinishResult(Event theEvent, Participant p, long seconds) =>
            new(theEvent.Identifier, Constants.Timing.TIMERESULT_DUMMYREAD, p.EventSpecific.Identifier,
                Constants.Timing.LOCATION_FINISH, Constants.Timing.SEGMENT_FINISH, 1, seconds, 0, "", seconds, 0,
                DateTime.Now, p.Bib, Constants.Timing.TIMERESULT_STATUS_NONE, "");

        [AvaloniaFact]
        public async Task Results_grid_shows_finish_results_for_known_participants()
        {
            (Event theEvent, Participant ann, Participant cat) = Seed();
            Db.AddTimingResults([FinishResult(theEvent, ann, 1500), FinishResult(theEvent, cat, 2400)]);

            TimingPage page = OpenPage();
            TimingResultsPage results = Assert.IsType<TimingResultsPage>(Find<ContentControl>(page, "TimingFrame").Content);
            ListBox grid = Find<ListBox>(results, "UpdateListView");
            await WaitUntil(() => grid.ItemsSource!.Cast<TimeResult>().Count() == 2, because: "results to load");

            Assert.Equal(["1", "3"], grid.ItemsSource!.Cast<TimeResult>().Select(r => r.Bib).OrderBy(b => b));
        }

        [AvaloniaFact]
        public async Task Search_narrows_the_results_grid()
        {
            (Event theEvent, Participant ann, Participant cat) = Seed();
            Db.AddTimingResults([FinishResult(theEvent, ann, 1500), FinishResult(theEvent, cat, 2400)]);
            TimingPage page = OpenPage();
            TimingResultsPage results = (TimingResultsPage)Find<ContentControl>(page, "TimingFrame").Content!;
            ListBox grid = Find<ListBox>(results, "UpdateListView");
            await WaitUntil(() => grid.ItemsSource!.Cast<TimeResult>().Count() == 2);

            Find<TextBox>(page, "SearchBox").Text = "Charlie";
            results.UpdateView();
            await WaitUntil(() => grid.ItemsSource!.Cast<TimeResult>().Count() == 1, because: "the search to apply");

            Assert.Equal("3", grid.ItemsSource!.Cast<TimeResult>().Single().Bib);
        }
    }
}
