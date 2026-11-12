using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Interfaces.UI;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.MainPages.Dashboard;
using Chronokeep.UI.MainPages.Timing;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// Every main page builds, renders, refreshes, and closes against a seeded event without throwing.
    /// These catch broken XAML, missing resources, and null handling before deeper behaviour is tested.
    /// </summary>
    public class MainPageSmokeTests : UiTestBase
    {
        public static TheoryData<string> MainPages =>
        [
            "About", "AgeGroups", "ChipAssignment", "Api", "Dashboard", "Distances",
            "Locations", "Participants", "Segments", "Settings", "Timing", "MinTiming",
        ];

        private Control CreatePage(string name) => name switch
        {
            "About" => new AboutPage(MainWindow, Db),
            "AgeGroups" => new AgeGroupsPage(MainWindow, Db),
            "ChipAssignment" => new ChipAssignmentPage(MainWindow, Db),
            "Api" => new ApiPage(MainWindow, Db),
            "Dashboard" => new DashboardPage(MainWindow, Db),
            "Distances" => new DistancesPage(MainWindow, Db),
            "Locations" => new LocationsPage(MainWindow, Db),
            "Participants" => new ParticipantsPage(MainWindow, Db),
            "Segments" => new SegmentsPage(MainWindow, Db),
            "Settings" => new SettingsPage(MainWindow, Db),
            "Timing" => new TimingPage(MainWindow, Db),
            "MinTiming" => new MinTimingPage(MainWindow, Db),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };

        [AvaloniaTheory]
        [MemberData(nameof(MainPages))]
        public async Task Page_renders_refreshes_and_closes_with_a_seeded_event(string name)
        {
            SeedEvent();
            Control page = CreatePage(name);
            Window host = Host(page);
            IMainPage mainPage = Assert.IsAssignableFrom<IMainPage>(page);

            mainPage.UpdateView();
            await Settle();
            mainPage.KeyboardCtrlZ();
            await Settle();
            mainPage.Closing();
            Pump();

            Assert.True(host.IsVisible);
            Assert.True(page.IsMeasureValid, $"{name} page never completed layout.");
        }

        public static TheoryData<string> PagesWithoutAnEvent => ["About", "Api", "Dashboard", "Settings"];

        [AvaloniaTheory]
        [MemberData(nameof(PagesWithoutAnEvent))]
        public async Task Page_renders_when_no_event_exists(string name)
        {
            Control page = CreatePage(name);
            Host(page);
            ((IMainPage)page).UpdateView();
            await Settle();
            Assert.True(page.IsMeasureValid);
        }

        public static TheoryData<string> TimingSubPages =>
            ["Alarms", "Award", "DistanceStats", "Print", "RawReads", "Results"];

        [AvaloniaTheory]
        [MemberData(nameof(TimingSubPages))]
        public async Task Timing_sub_page_renders_inside_the_timing_page(string name)
        {
            (_, Objects.Distance distance) = SeedEvent();
            TimingPage timing = new(MainWindow, Db);
            Host(timing);
            Control sub = name switch
            {
                "Alarms" => new AlarmsPage(timing, Db),
                "Award" => new AwardPage(timing, Db),
                "DistanceStats" => new DistanceStatsPage(timing, MainWindow, Db, distance.Identifier, distance.Name),
                "Print" => new PrintPage(timing, Db),
                "RawReads" => new TimingRawReadsPage(timing, Db, MainWindow),
                "Results" => new TimingResultsPage(timing, Db),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
            };
            Host(sub);
            await Settle();
            Assert.True(sub.IsMeasureValid, $"{name} sub-page never completed layout.");
        }
    }
}
