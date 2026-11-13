using Chronokeep.Helpers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.IO;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.Export;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.MainPages.Timing;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// The results output screens: Distance Stats, Awards and Print sub-pages, and the per-distance
    /// exports (RunSignup, UltraSignup, Abbott WMM, Boston) written to real files.
    /// </summary>
    public class ResultsOutputTests : UiTestBase
    {
        private TimingPage OpenTiming()
        {
            TimingPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        private static T ShowSubPage<T>(TimingPage timing, T page) where T : Control
        {
            Find<ContentControl>(timing, "TimingFrame").Content = page;
            Pump();
            return page;
        }

        private Participant Runner(Event e, Distance d, string bib, string first, int status)
        {
            Participant p = AddParticipant(e.Identifier, d, bib, first: first);
            p.EventSpecific.Status = status;
            Db.UpdateParticipant(p);
            return p;
        }

        [AvaloniaFact]
        public void Distance_stats_sort_runners_by_status_and_hide_empty_groups()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            Runner(e, fiveK, "1", "Active", Constants.Timing.EVENTSPECIFIC_STARTED);
            Runner(e, fiveK, "2", "Done", Constants.Timing.EVENTSPECIFIC_FINISHED);
            Runner(e, fiveK, "3", "Quit", Constants.Timing.EVENTSPECIFIC_DNF);
            TimingPage timing = OpenTiming();

            DistanceStatsPage page = ShowSubPage(timing, new DistanceStatsPage(timing, MainWindow, Db, fiveK.Identifier, "5K"));

            Assert.Equal("5K", Find<TextBlock>(page, "DistanceName").Text);
            Assert.Equal("Active", Assert.Single(Find<DataGrid>(page, "ActiveListView").ItemsSource!.Cast<StatsParticipant>()).GetParticipant().FirstName);
            Assert.Equal("Done", Assert.Single(Find<DataGrid>(page, "FinishedListView").ItemsSource!.Cast<Participant>()).FirstName);
            Assert.Equal("Quit", Assert.Single(Find<DataGrid>(page, "DnfListView").ItemsSource!.Cast<Participant>()).FirstName);
            Assert.False(Find<Expander>(page, "DnsPanel").IsVisible);
            Assert.False(Find<Expander>(page, "UnknownPanel").IsVisible);
        }

        [AvaloniaFact]
        public void Condensed_distance_stats_include_linked_distances()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance half = AddDistance(e.Identifier, "Half");
            Distance early = AddDistance(e.Identifier, "Half Early");
            early.LinkedDistance = half.Identifier;
            Db.UpdateDistance(early);
            Runner(e, half, "1", "Main", Constants.Timing.EVENTSPECIFIC_FINISHED);
            Runner(e, early, "2", "Early", Constants.Timing.EVENTSPECIFIC_FINISHED);
            TimingPage timing = OpenTiming();

            DistanceStatsPage page = ShowSubPage(timing, new DistanceStatsPage(timing, MainWindow, Db, half.Identifier, "Half", condensed: true));

            Assert.Equal(["Early", "Main"], Find<DataGrid>(page, "FinishedListView").ItemsSource!.Cast<Participant>().Select(p => p.FirstName).Order());
        }

        [AvaloniaFact]
        public void Distance_stats_leave_out_participants_removed_in_runsignup()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            Runner(e, fiveK, "1", "Kept", Constants.Timing.EVENTSPECIFIC_FINISHED);
            Participant removed = Runner(e, fiveK, "2", "Gone", Constants.Timing.EVENTSPECIFIC_FINISHED);
            removed.EventSpecific.Removed = "removed";
            Db.UpdateParticipant(removed);
            TimingPage timing = OpenTiming();

            DistanceStatsPage page = ShowSubPage(timing, new DistanceStatsPage(timing, MainWindow, Db, fiveK.Identifier, "5K"));

            Assert.Equal("Kept", Assert.Single(Find<DataGrid>(page, "FinishedListView").ItemsSource!.Cast<Participant>()).FirstName);
        }

        [AvaloniaFact]
        public void Distance_stats_done_returns_to_the_results_view()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance d = AddDistance(e.Identifier);
            TimingPage timing = OpenTiming();
            DistanceStatsPage page = ShowSubPage(timing, new DistanceStatsPage(timing, MainWindow, Db, d.Identifier, d.Name));

            Click(Find<Button>(page, "DoneButton"));

            Assert.IsType<TimingResultsPage>(Find<ContentControl>(timing, "TimingFrame").Content);
        }

        private AwardPage OpenAwards(Event e, out TimingPage timing)
        {
            timing = OpenTiming();
            return ShowSubPage(timing, new AwardPage(timing, Db));
        }

        private List<AgeGroup> CustomGroups(Event e) => Db.GetAgeGroups(e.Identifier, Constants.Timing.AGEGROUPS_CUSTOM_DISTANCEID);

        private static void AddCustomGroup(AwardPage page, string start, string end, string name)
        {
            Find<TextBox>(page, "StartCustom").Text = start;
            Find<TextBox>(page, "EndCustom").Text = end;
            Find<TextBox>(page, "CustomNameBox").Text = name;
            Click(Find<Button>(page, "AddCustom"));
        }

        [AvaloniaFact]
        public void Awards_lists_only_main_distances()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance half = AddDistance(e.Identifier, "Half");
            AddDistance(e.Identifier, "10K");
            Distance early = AddDistance(e.Identifier, "Half Early");
            early.LinkedDistance = half.Identifier;
            Db.UpdateDistance(early);

            AwardPage page = OpenAwards(e, out _);

            Assert.Equal(["All", "10K", "Half"], Find<ListBox>(page, "DistancesBox").Items.OfType<ListBoxItem>().Select(i => (string)i.Content!));
        }

        [AvaloniaFact]
        public void Awards_custom_groups_are_added_and_deleted()
        {
            Event e = AddEvent(makeCurrent: true);
            AwardPage page = OpenAwards(e, out _);

            AddCustomGroup(page, "40", "99", "Masters");
            AgeGroup masters = Assert.Single(CustomGroups(e));
            Assert.Equal((40, 99, "Masters"), (masters.StartAge, masters.EndAge, masters.CustomName));
            Assert.Single(Find<DataGrid>(page, "CustomGroupsListView").ItemsSource!.Cast<AgeGroup>());

            DataGrid list = Find<DataGrid>(page, "CustomGroupsListView");
            list.SelectedItems.Add(list.ItemsSource!.Cast<AgeGroup>().Single());
            Click(Find<Button>(page, "DeleteCustom"));
            Assert.Empty(CustomGroups(e));
        }

        [AvaloniaTheory]
        [InlineData("50", "200")]
        [InlineData("30", "20")]
        [InlineData("-1", "10")]
        public void Awards_custom_groups_outside_zero_to_one_hundred_or_backwards_are_refused(string start, string end)
        {
            Event e = AddEvent(makeCurrent: true);
            AwardPage page = OpenAwards(e, out _);

            AddCustomGroup(page, start, end, "Bad");

            Assert.Empty(CustomGroups(e));
        }

        [AvaloniaFact]
        public void Awards_and_print_done_return_to_the_results_view()
        {
            Event e = AddEvent(makeCurrent: true);
            AddDistance(e.Identifier);
            AwardPage awards = OpenAwards(e, out TimingPage timing);
            Click(Find<Button>(awards, "DoneButton"));
            Assert.IsType<TimingResultsPage>(Find<ContentControl>(timing, "TimingFrame").Content);

            PrintPage print = ShowSubPage(timing, new PrintPage(timing, Db));
            Assert.Equal(["All", "5K"], Find<ListBox>(print, "DistancesBox").Items.OfType<ListBoxItem>().Select(i => (string)i.Content!));
            Click(Find<Button>(print, "Done"));
            Assert.IsType<TimingResultsPage>(Find<ContentControl>(timing, "TimingFrame").Content);
        }

        /// <summary>A Male/Female event with a finisher and a DNF in the 5K and a finisher in the 10K.</summary>
        private Event SeedResults()
        {
            Event e = AddEvent(makeCurrent: true);
            e.UseMaleFemale = true;
            Db.UpdateEvent(e);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            Distance tenK = AddDistance(e.Identifier, "10K");
            Participant fran = AddParticipant(e.Identifier, fiveK, "1", first: "Fran", last: "Fast", birthday: "1990/01/02", gender: "Female");
            Participant max = AddParticipant(e.Identifier, fiveK, "2", first: "Max", last: "Slow", birthday: "1985/07/08", gender: "Male");
            Participant ten = AddParticipant(e.Identifier, tenK, "3", first: "Tess", last: "Ten", gender: "Female");
            Participant nico = AddParticipant(e.Identifier, fiveK, "4", first: "Nico", last: "Neutral", birthday: "1995/11/12", gender: "Non-Binary");
            TimeResult Finish(Participant p, long seconds, int place, int status = Constants.Timing.TIMERESULT_STATUS_NONE) =>
                new(e.Identifier, Constants.Timing.TIMERESULT_DUMMYREAD, p.EventSpecific.Identifier,
                    Constants.Timing.LOCATION_FINISH, Constants.Timing.SEGMENT_FINISH, 1, seconds, 0, "", seconds, 0,
                    DateTime.Now, p.Bib, status, "") { Place = place };
            Db.AddTimingResults([
                Finish(fran, 1500, 1),
                Finish(max, 0, 0, Constants.Timing.TIMERESULT_STATUS_DNF),
                Finish(ten, 3000, 1),
                Finish(nico, 1600, 2),
            ]);
            return Db.GetCurrentEvent()!;
        }

        /// <summary>Every runner row's gender is M, F or X: never W, and nothing left blank for these runners.</summary>
        private static void AssertGendersAreMfx(List<string[]> csv, int headerRow, string genderColumn)
        {
            int ix = Array.IndexOf(csv[headerRow], genderColumn);
            List<string> genders = [.. csv.Skip(headerRow + 1).Where(r => r.Length > ix).Select(r => r[ix])];
            Assert.NotEmpty(genders);
            Assert.All(genders, g => Assert.Contains(g, new[] { "M", "F", "X" }));
        }

        private ExportDistanceResults OpenExport(OutputType type) => Show(new ExportDistanceResults(MainWindow, Db, type));

        private string OutputPath(string name) => Path.Combine(Path.GetDirectoryName(DbPath)!, name);

        private static List<string[]> ReadCsv(string path) => [.. File.ReadAllLines(path).Select(CsvImporter.ParseLine)];

        private static Dictionary<string, string> Row(List<string[]> csv, int headerRow, Func<Dictionary<string, string>, bool> which)
        {
            string[] headers = csv[headerRow];
            return csv.Skip(headerRow + 1)
                .Select(r => headers.Select((h, i) => (h, v: i < r.Length ? r[i] : "")).ToDictionary(x => x.h, x => x.v))
                .Single(which);
        }

        [AvaloniaFact]
        public void Gender_codes_are_m_f_or_x_for_both_label_styles()
        {
            Assert.Equal("M", ExportDistanceResults.GenderCode("Male"));
            Assert.Equal("M", ExportDistanceResults.GenderCode("Man"));
            Assert.Equal("F", ExportDistanceResults.GenderCode("Female"));
            Assert.Equal("F", ExportDistanceResults.GenderCode("woman"));
            Assert.Equal("X", ExportDistanceResults.GenderCode("Non-Binary"));
            Assert.Equal("", ExportDistanceResults.GenderCode("Not Specified"));
        }

        [AvaloniaFact]
        public void RunSignup_export_lists_the_distances_finishers_with_genders()
        {
            Event e = SeedResults();
            string path = OutputPath("runsignup.csv");

            OpenExport(OutputType.RunSignup).SaveRunSignupInternal("5K", path);

            List<string[]> csv = ReadCsv(path);
            Assert.Equal(["place", "clock time", "chip time", "first", "last", "gender", "age", "bib", "city", "state"], csv[0]);
            Assert.Equal(3, csv.Count - 1);
            Dictionary<string, string> fran = Row(csv, 0, r => r["bib"] == "1");
            Assert.Equal(("1", "Fran", "F", "Springfield", "OR"), (fran["place"], fran["first"], fran["gender"], fran["city"], fran["state"]));
            Assert.Equal("M", Row(csv, 0, r => r["bib"] == "2")["gender"]);
            Assert.Equal("X", Row(csv, 0, r => r["bib"] == "4")["gender"]);
            AssertGendersAreMfx(csv, 0, "gender");
            Assert.DoesNotContain(csv, r => r.Contains("Tess"));
        }

        [AvaloniaFact]
        public void UltraSignup_export_marks_dnfs_and_writes_month_day_year_birthdates()
        {
            SeedResults();
            string path = OutputPath("ultrasignup.csv");

            OpenExport(OutputType.UltraSignup).SaveUltraSignupInternal("5K", path);

            List<string[]> csv = ReadCsv(path);
            Dictionary<string, string> fran = Row(csv, 0, r => r["bib"] == "1");
            Assert.Equal(("01/02/1990", "F", "1"), (fran["dob"], fran["gender"], fran["status"]));
            Dictionary<string, string> max = Row(csv, 0, r => r["bib"] == "2");
            Assert.Equal(("07/08/1985", "M", "2"), (max["dob"], max["gender"], max["status"]));
            Assert.Equal("X", Row(csv, 0, r => r["bib"] == "4")["gender"]);
            AssertGendersAreMfx(csv, 0, "gender");
        }

        [AvaloniaFact]
        public void Abbott_export_writes_month_day_year_birthdates_and_m_f_or_x()
        {
            SeedResults();
            string path = OutputPath("abbott.csv");

            OpenExport(OutputType.Abbott).SaveAbbotInternal("5K", path, ".csv");

            List<string[]> csv = ReadCsv(path);
            Dictionary<string, string> fran = Row(csv, 0, r => r["start_num"] == "1");
            Assert.Equal(("Fran", "Fast", "01/02/1990", "F"), (fran["first_name"], fran["last_name"], fran["date_of_birth"], fran["gender"]));
            Assert.Equal("X", Row(csv, 0, r => r["start_num"] == "4")["gender"]);
            AssertGendersAreMfx(csv, 0, "gender");
        }

        [AvaloniaFact]
        public void Boston_export_writes_month_day_year_birthdates_and_genders()
        {
            SeedResults();
            string path = OutputPath("boston.csv");

            OpenExport(OutputType.Boston).SaveBostonInternal("5K", path, ".csv");

            List<string[]> csv = ReadCsv(path);
            int header = csv.FindIndex(r => r.Length > 0 && r[0] == "Last Name");
            Dictionary<string, string> fran = Row(csv, header, r => r["First Name"] == "Fran");
            Assert.Equal(("01/02/1990", "F"), (fran["Date of Birth"], fran["Gender"]));
            Assert.Equal(EventDates.ToMonthDayYear(Db.GetCurrentEvent()!.Date), csv[1][0]);
            Assert.Matches(@"^\d{2}/\d{2}/\d{4}$", csv[1][0]);
            Assert.Equal("X", Row(csv, header, r => r["First Name"] == "Nico")["Gender"]);
            AssertGendersAreMfx(csv, header, "Gender");
        }
    }
}
