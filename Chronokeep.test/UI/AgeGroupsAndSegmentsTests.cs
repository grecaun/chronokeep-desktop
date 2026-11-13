using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.Parts;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// The Age Groups and Segments pages: editing, saving, and when leaving the page recalculates
    /// (and clears uploaded) results.
    /// </summary>
    public class AgeGroupsAndSegmentsTests : UiTestBase
    {
        private Event SeedEvent(bool commonAgeGroups = true, bool distanceSpecificSegments = false, int finishMaxOccurrences = 3)
        {
            Event e = AddEvent(makeCurrent: true);
            e.CommonAgeGroups = commonAgeGroups;
            e.DistanceSpecificSegments = distanceSpecificSegments;
            e.FinishMaxOccurrences = finishMaxOccurrences;
            Db.UpdateEvent(e);
            return Db.GetCurrentEvent()!;
        }

        private AgeGroupsPage OpenAgeGroups()
        {
            AgeGroupsPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        private static List<AgeGroupPart> AgeRows(AgeGroupsPage page) =>
            [.. Find<ListBox>(page, "AgeGroupsBox").Items.OfType<AgeGroupPart>()];

        private static List<(int, int)> Ranges(IEnumerable<AgeGroup> groups) =>
            [.. groups.Select(g => (g.StartAge, g.EndAge)).Order()];

        private static void SetRow(AgeGroupPart row, int start, int end)
        {
            Find<TextBox>(row, "StartAge").Text = start.ToString();
            Find<TextBox>(row, "EndAge").Text = end.ToString();
        }

        [AvaloniaFact]
        public void Default_ten_year_groups_cover_zero_to_ninety_nine()
        {
            Event e = SeedEvent();
            AgeGroupsPage page = OpenAgeGroups();

            Find<ComboBox>(page, "DefaultGroupsBox").SelectedIndex = 0;
            Click(Find<Button>(page, "AddDefault"));

            List<(int, int)> saved = Ranges(Db.GetAgeGroups(e.Identifier, Constants.Timing.COMMON_AGEGROUPS_DISTANCEID));
            Assert.Equal(10, saved.Count);
            Assert.Equal((0, 9), saved[0]);
            Assert.Equal((90, 99), saved[^1]);
            Assert.Equal(10, AgeRows(page).Count);
        }

        [AvaloniaFact]
        public void Saving_added_groups_fills_the_gaps_from_zero()
        {
            Event e = SeedEvent();
            AgeGroupsPage page = OpenAgeGroups();
            Click(Find<Button>(page, "Add"));
            Click(Find<Button>(page, "Add"));
            SetRow(AgeRows(page)[0], 20, 29);
            SetRow(AgeRows(page)[1], 40, 49);

            Click(Find<Button>(page, "Save"));

            Assert.Equal([(0, 19), (20, 29), (30, 39), (40, 49)],
                Ranges(Db.GetAgeGroups(e.Identifier, Constants.Timing.COMMON_AGEGROUPS_DISTANCEID)));
        }

        [AvaloniaFact]
        public void Overlapping_groups_are_not_saved()
        {
            Event e = SeedEvent();
            AgeGroupsPage page = OpenAgeGroups();
            Click(Find<Button>(page, "Add"));
            Click(Find<Button>(page, "Add"));
            SetRow(AgeRows(page)[0], 0, 29);
            SetRow(AgeRows(page)[1], 25, 49);

            Click(Find<Button>(page, "Save"));

            Assert.Empty(Db.GetAgeGroups(e.Identifier, Constants.Timing.COMMON_AGEGROUPS_DISTANCEID));
        }

        [AvaloniaFact]
        public void Removing_a_row_and_saving_deletes_that_group()
        {
            Event e = SeedEvent();
            Db.AddAgeGroup(new AgeGroup(e.Identifier, Constants.Timing.COMMON_AGEGROUPS_DISTANCEID, 0, 39));
            Db.AddAgeGroup(new AgeGroup(e.Identifier, Constants.Timing.COMMON_AGEGROUPS_DISTANCEID, 40, 99));
            AgeGroupsPage page = OpenAgeGroups();

            Click(Find<Button>(AgeRows(page).Single(r => Find<TextBox>(r, "StartAge").Text == "40"), "RemoveButton"));
            Click(Find<Button>(page, "Save"));

            Assert.Equal([(0, 39)], Ranges(Db.GetAgeGroups(e.Identifier, Constants.Timing.COMMON_AGEGROUPS_DISTANCEID)));
        }

        [AvaloniaFact]
        public void Reset_drops_unsaved_rows()
        {
            SeedEvent();
            AgeGroupsPage page = OpenAgeGroups();
            Click(Find<Button>(page, "Add"));

            Click(Find<Button>(page, "ResetBtn"));

            Assert.Empty(AgeRows(page));
        }

        [AvaloniaFact]
        public void Each_distance_keeps_its_own_groups_when_switching()
        {
            Event e = SeedEvent(commonAgeGroups: false);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            Distance tenK = AddDistance(e.Identifier, "10K");
            Db.AddAgeGroup(new AgeGroup(e.Identifier, tenK.Identifier, 0, 99));
            AgeGroupsPage page = OpenAgeGroups();
            ComboBox distances = Find<ComboBox>(page, "DistancesBox");
            distances.SelectedItem = distances.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag! == fiveK.Identifier.ToString());
            Pump();
            Click(Find<Button>(page, "Add"));
            SetRow(AgeRows(page).Single(), 0, 49);

            distances.SelectedItem = distances.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag! == tenK.Identifier.ToString());
            Pump();

            Assert.Equal([(0, 49)], Ranges(Db.GetAgeGroups(e.Identifier, fiveK.Identifier)));
            Assert.Equal([(0, 99)], Ranges(Db.GetAgeGroups(e.Identifier, tenK.Identifier)));
            Assert.Equal("99", Find<TextBox>(AgeRows(page).Single(), "EndAge").Text);
        }

        [AvaloniaFact]
        public void Leaving_age_groups_without_changes_does_not_clear_results()
        {
            Event e = SeedEvent(commonAgeGroups: false);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            Db.AddAgeGroup(new AgeGroup(e.Identifier, fiveK.Identifier, 0, 99));
            AgeGroupsPage page = OpenAgeGroups();

            page.Closing();

            Assert.Equal(0, MainWindow.NetworkClearResultsCalls);
            Assert.Equal([(0, 99)], Ranges(Db.GetAgeGroups(e.Identifier, fiveK.Identifier)));
        }

        [AvaloniaFact]
        public void Leaving_after_changing_groups_assigns_them_and_recalculates()
        {
            Event e = SeedEvent();
            Distance d = AddDistance(e.Identifier);
            Participant runner = AddParticipant(e.Identifier, d, "1", birthday: "1990/01/02");
            AgeGroupsPage page = OpenAgeGroups();
            Find<ComboBox>(page, "DefaultGroupsBox").SelectedIndex = 0;
            Click(Find<Button>(page, "AddDefault"));

            page.Closing();

            int age = runner.GetAge(e.Date);
            Participant updated = Db.GetParticipantEventSpecific(e.Identifier, runner.EventSpecific.Identifier)!;
            Assert.Equal($"{age / 10 * 10}-{age / 10 * 10 + 9}", updated.EventSpecific.AgeGroupName);
            Assert.Equal(1, MainWindow.NetworkClearResultsCalls);
            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);
        }

        private SegmentsPage OpenSegments()
        {
            SegmentsPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        private static List<SegmentPart> SegmentRows(SegmentsPage page) =>
            [.. (Find<ItemsControl>(page, "SegmentsBox").ItemsSource ?? Array.Empty<object>()).OfType<SegmentPart>()];

        private static DistanceSegmentHolderPart Holder(SegmentsPage page) =>
            (Find<ItemsControl>(page, "SegmentsBox").ItemsSource ?? Array.Empty<object>()).OfType<DistanceSegmentHolderPart>().First();

        private int AddSavedSegment(Event e, string name, int occurrence = 0) =>
            Db.AddSegment(new Segment(e.Identifier, Constants.Timing.COMMON_SEGMENTS_DISTANCEID, Constants.Timing.LOCATION_FINISH,
                occurrence, 0.0, 3.1, Constants.Distances.MILES, name, "", ""));

        [AvaloniaFact]
        public void Adding_a_segment_and_updating_saves_it()
        {
            Event e = SeedEvent();
            SegmentsPage page = OpenSegments();

            Click(Find<Button>(Holder(page), "AddButton"));
            SegmentPart row = Assert.Single(SegmentRows(page));
            Find<TextBox>(row, "SegName").Text = "Halfway";
            Click(Find<Button>(page, "UpdateBtn"));

            Segment saved = Assert.Single(Db.GetSegments(e.Identifier));
            Assert.Equal("Halfway", saved.Name);
            Assert.Equal(Constants.Timing.LOCATION_FINISH, saved.LocationId);
        }

        [AvaloniaFact]
        public void Editing_a_segment_saves_it_and_recalculates_when_leaving()
        {
            Event e = SeedEvent();
            AddSavedSegment(e, "Halfway");
            SegmentsPage page = OpenSegments();

            Find<TextBox>(Assert.Single(SegmentRows(page)), "SegName").Text = "Mile 13";
            Click(Find<Button>(page, "UpdateBtn"));
            page.Closing();

            Assert.Equal("Mile 13", Assert.Single(Db.GetSegments(e.Identifier)).Name);
            Assert.Equal(1, MainWindow.NetworkClearResultsCalls);
        }

        [AvaloniaFact]
        public void Leaving_segments_without_changes_does_not_clear_results()
        {
            Event e = SeedEvent();
            AddSavedSegment(e, "Halfway");
            SegmentsPage page = OpenSegments();

            Click(Find<Button>(page, "UpdateBtn"));
            page.Closing();

            Assert.Equal(0, MainWindow.NetworkClearResultsCalls);
            Assert.Equal(0, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public void Removing_a_segment_deletes_it_and_recalculates_when_leaving()
        {
            Event e = SeedEvent();
            AddSavedSegment(e, "Halfway");
            SegmentsPage page = OpenSegments();

            Click(Find<Button>(Assert.Single(SegmentRows(page)), "RemoveButton"));
            page.Closing();

            Assert.Empty(Db.GetSegments(e.Identifier));
            Assert.Empty(SegmentRows(page));
            Assert.Equal(1, MainWindow.NetworkClearResultsCalls);
        }

        [AvaloniaFact]
        public void Reset_discards_unsaved_segment_edits()
        {
            Event e = SeedEvent();
            AddSavedSegment(e, "Halfway");
            SegmentsPage page = OpenSegments();
            Find<TextBox>(Assert.Single(SegmentRows(page)), "SegName").Text = "Typed but reset";

            Click(Find<Button>(page, "ResetBtn"));

            Assert.Equal("Halfway", Find<TextBox>(Assert.Single(SegmentRows(page)), "SegName").Text);
            Assert.Equal("Halfway", Assert.Single(Db.GetSegments(e.Identifier)).Name);
        }

        [AvaloniaFact]
        public void Distance_specific_segments_show_a_section_per_distance()
        {
            Event e = SeedEvent(distanceSpecificSegments: true);
            AddDistance(e.Identifier, "5K");
            AddDistance(e.Identifier, "10K");

            SegmentsPage page = OpenSegments();

            List<DistanceSegmentHolderPart> holders = [.. Find<ItemsControl>(page, "SegmentsBox").ItemsSource!.OfType<DistanceSegmentHolderPart>()];
            Assert.Equal(2, holders.Count);
        }
    }
}
