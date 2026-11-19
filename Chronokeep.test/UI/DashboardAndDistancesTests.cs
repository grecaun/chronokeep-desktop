using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.Parts;

namespace Chronokeep.Test.UI
{
    /// <summary>Editing the event on the Dashboard, and managing distances on the Distances page.</summary>
    public class DashboardAndDistancesTests : UiTestBase
    {
        private DashboardPage OpenDashboard()
        {
            DashboardPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        [AvaloniaFact]
        public void Dashboard_fields_are_read_only_until_edit_is_pressed()
        {
            SeedEvent("Spring Fling");
            DashboardPage page = OpenDashboard();
            TextBox name = Find<TextBox>(page, "EventNameTextBox");
            Button edit = Find<Button>(page, "EditButton");
            Assert.Equal("Spring Fling", name.Text);
            Assert.False(name.IsEnabled);
            Assert.Equal(Constants.DashboardLabels.EDIT, ContentText(edit));

            Click(edit);

            Assert.True(name.IsEnabled);
            Assert.Equal(Constants.DashboardLabels.SAVE, ContentText(edit));
            Assert.True(Find<Button>(page, "CancelButton").IsVisible);
        }

        [AvaloniaFact]
        public void Dashboard_save_stores_the_edited_event()
        {
            (Event e, _) = SeedEvent("Spring Fling");
            DashboardPage page = OpenDashboard();
            Click(Find<Button>(page, "EditButton"));

            Find<TextBox>(page, "EventNameTextBox").Text = "Spring Fling 10K";
            Find<TextBox>(page, "EventYearCodeTextBox").Text = "2027";
            Find<CalendarDatePicker>(page, "EventDatePicker").SelectedDate = new DateTime(2027, 4, 18);
            Find<ComboBox>(page, "RankBox").SelectedIndex = 1;
            Find<ToggleSwitch>(page, "PlacementsCheckBox").IsChecked = false;
            Click(Find<Button>(page, "EditButton"));

            Event saved = Db.GetEvent(e.Identifier)!;
            Assert.Equal(("Spring Fling 10K", "2027"), (saved.Name, saved.YearCode));
            Assert.Equal("2027/04/18", saved.Date);
            Assert.Equal(RankingType.Chip, saved.RankedBy);
            Assert.False(saved.DisplayPlacements);
            Assert.Equal(Constants.DashboardLabels.EDIT, ContentText(Find<Button>(page, "EditButton")));
            Assert.False(Find<TextBox>(page, "EventNameTextBox").IsEnabled);
            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public void Dashboard_cancel_puts_the_fields_back()
        {
            (Event e, _) = SeedEvent("Spring Fling");
            DashboardPage page = OpenDashboard();
            Click(Find<Button>(page, "EditButton"));
            Find<TextBox>(page, "EventNameTextBox").Text = "Typed but cancelled";

            Click(Find<Button>(page, "CancelButton"));

            Assert.Equal("Spring Fling", Find<TextBox>(page, "EventNameTextBox").Text);
            Assert.Equal("Spring Fling", Db.GetEvent(e.Identifier)!.Name);
            Assert.False(Find<Button>(page, "CancelButton").IsVisible);
        }

        [AvaloniaFact]
        public void Switching_to_male_female_relabels_every_participant()
        {
            (Event e, Distance d) = SeedEvent();
            AddParticipant(e.Identifier, d, "200", first: "Max", gender: "M");
            DashboardPage page = OpenDashboard();
            Click(Find<Button>(page, "EditButton"));

            Find<ComboBox>(page, "GenderBox").SelectedIndex = 1;
            Click(Find<Button>(page, "EditButton"));

            Assert.True(Db.GetEvent(e.Identifier)!.UseMaleFemale);
            Dictionary<string, string> genders = Db.GetParticipants(e.Identifier).ToDictionary(p => p.FirstName, p => p.Gender);
            Assert.Equal("Female", genders["Jane"]);
            Assert.Equal("Male", genders["Max"]);
        }

        [AvaloniaFact]
        public void Switching_genders_rebuilds_results_and_other_saves_keep_them()
        {
            (Event e, Distance d) = SeedEvent();
            Participant max = AddParticipant(e.Identifier, d, "200", first: "Max", gender: "M");
            Db.AddChipReads([NewRead(e.Identifier, "200", 1800)]);
            int readId = Db.GetChipReads(e.Identifier).Single().ReadId;
            Db.AddTimingResult(new TimeResult(e.Identifier, readId, max.EventSpecific.Identifier,
                Constants.Timing.LOCATION_FINISH, Constants.Timing.SEGMENT_FINISH, 1, 1800, 0, "", 1800, 0,
                DateTime.Now, "200", Constants.Timing.TIMERESULT_STATUS_NONE, ""));
            string results = $"event_id={e.Identifier}";
            DashboardPage page = OpenDashboard();

            // Saving without touching the gender option leaves results alone.
            Click(Find<Button>(page, "EditButton"));
            Click(Find<Button>(page, "EditButton"));
            Assert.Equal(1, Count("time_results", results));

            // Results hold a copy of each runner's gender, so they're cleared for the timing worker to build again.
            Click(Find<Button>(page, "EditButton"));
            Find<ComboBox>(page, "GenderBox").SelectedIndex = 1;
            Click(Find<Button>(page, "EditButton"));
            Assert.Equal(0, Count("time_results", results));
            Assert.Equal(2, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaTheory]
        [InlineData("de-DE")]
        [InlineData("en-GB")]
        [InlineData("fi-FI")]
        public void Editing_the_event_keeps_its_date_on_any_computer(string culture)
        {
            Event e = AddEvent(date: "6/5/2026", makeCurrent: true);
            Chronokeep.Test.Timing.CultureScope.In(culture, () =>
            {
                DashboardPage page = OpenDashboard();
                Assert.Equal(new DateTime(2026, 6, 5), Find<CalendarDatePicker>(page, "EventDatePicker").SelectedDate?.Date);

                Click(Find<Button>(page, "EditButton"));
                Click(Find<Button>(page, "EditButton"));

                Assert.Equal("2026/06/05", Db.GetEvent(e.Identifier)!.Date);
            });
        }

        private DistancesPage OpenDistances()
        {
            DistancesPage page = new(MainWindow, Db);
            Host(page);
            page.UpdateView();
            Pump();
            return page;
        }

        private static List<DistancePart> Rows(DistancesPage page) =>
            [.. Find<ListBox>(page, "DistancesBox").Items.OfType<DistancePart>()];

        private static DistancePart Row(DistancesPage page, string name) =>
            Rows(page).Single(r => r.GetDistance().Name == name);

        [AvaloniaFact]
        public void Distances_page_lists_each_distance_with_linked_distances_after_their_parent()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance half = AddDistance(e.Identifier, "Half");
            AddDistance(e.Identifier, "10K");
            Distance early = AddDistance(e.Identifier, "Half Early");
            early.LinkedDistance = half.Identifier;
            Db.UpdateDistance(early);

            List<DistancePart> rows = Rows(OpenDistances());

            List<string> names = [.. rows.Select(r => r.GetDistance().Name)];
            Assert.Equal(3, names.Count);
            Assert.Equal(names.IndexOf("Half") + 1, names.IndexOf("Half Early"));
            Assert.False(Find<Button>(rows.Single(r => r.GetDistance().Name == "Half Early"), "AddSubButton").IsEffectivelyVisible);
        }

        [AvaloniaFact]
        public void Add_creates_a_new_distance_row()
        {
            Event e = AddEvent(makeCurrent: true);
            AddDistance(e.Identifier, "5K");
            DistancesPage page = OpenDistances();

            Click(Find<Button>(page, "Add"));

            Assert.Equal(2, Rows(page).Count);
            Assert.Contains(Db.GetDistances(e.Identifier), d => d.Name.StartsWith("New Distance"));
        }

        [AvaloniaFact]
        public void Update_saves_edited_names_and_lengths_and_reset_discards_them()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            DistancesPage page = OpenDistances();

            Find<TextBox>(Row(page, "5K"), "DistanceName").Text = "Typed but reset";
            Click(Find<Button>(page, "ResetBtn"));
            Assert.Equal("5K", Db.GetDistance(fiveK.Identifier)!.Name);
            Assert.Equal("5K", Find<TextBox>(Row(page, "5K"), "DistanceName").Text);

            DistancePart row = Row(page, "5K");
            Find<TextBox>(row, "DistanceName").Text = "5K Fun Run";
            Find<TextBox>(row, "DistanceBox").Text = "3.2";
            Click(Find<Button>(page, "UpdateBtn"));

            Distance saved = Db.GetDistance(fiveK.Identifier)!;
            Assert.Equal("5K Fun Run", saved.Name);
            Assert.Equal(3.2, saved.DistanceValue, 3);
            Assert.Equal(1, MainWindow.NetworkUpdateResultsCalls);
        }

        [AvaloniaFact]
        public void Add_linked_creates_a_linked_distance_under_its_parent()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance half = AddDistance(e.Identifier, "Half");
            DistancesPage page = OpenDistances();

            Click(Find<Button>(Row(page, "Half"), "AddSubButton"));

            Distance linked = Db.GetDistances(e.Identifier).Single(d => d.Identifier != half.Identifier);
            Assert.Equal(half.Identifier, linked.LinkedDistance);
            Assert.StartsWith("Half Linked", linked.Name);
            Assert.Equal(2, Rows(page).Count);
        }

        [AvaloniaFact]
        public void Remove_deletes_a_distance_and_its_linked_distances_when_nobody_is_in_them()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance half = AddDistance(e.Identifier, "Half");
            AddDistance(e.Identifier, "10K");
            Distance early = AddDistance(e.Identifier, "Half Early");
            early.LinkedDistance = half.Identifier;
            Db.UpdateDistance(early);
            DistancesPage page = OpenDistances();

            Click(Find<Button>(Row(page, "Half"), "RemoveButton"));

            Assert.Equal("10K", Assert.Single(Db.GetDistances(e.Identifier)).Name);
            Assert.Single(Rows(page));
        }

        [AvaloniaFact]
        public void Remove_asks_first_and_keeps_a_distance_that_has_participants()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            AddParticipant(e.Identifier, fiveK, "1");
            DistancesPage page = OpenDistances();

            // The confirmation dialog needs the real main window, so nothing is answered here.
            Click(Find<Button>(Row(page, "5K"), "RemoveButton"));

            Assert.Single(Db.GetDistances(e.Identifier));
            Assert.Single(Db.GetParticipants(e.Identifier));
        }

        [AvaloniaFact]
        public void Backyard_ultras_allow_only_one_distance()
        {
            Event e = AddEvent(makeCurrent: true);
            e.EventType = Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA;
            Db.UpdateEvent(e);
            AddDistance(e.Identifier, "Backyard");

            DistancesPage page = OpenDistances();

            Assert.False(Find<Button>(page, "Add").IsEnabled);
            Assert.False(Find<Button>(Row(page, "Backyard"), "RemoveButton").IsEffectivelyVisible);
        }
    }
}
